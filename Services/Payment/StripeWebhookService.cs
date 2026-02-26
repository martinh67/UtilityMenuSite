using Microsoft.Extensions.Options;
using Stripe;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Infrastructure.Security;
using StripeSubscription = Stripe.Subscription;

namespace UtilityMenuSite.Services.Payment;

public class StripeWebhookService : IStripeWebhookService
{
    private static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromDays(7);

    private readonly StripeSettings _settings;
    private readonly ILicenceService _licenceService;
    private readonly IUserService _userService;
    private readonly ILicenceRepository _licenceRepo;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(
        IOptions<StripeSettings> settings,
        ILicenceService licenceService,
        IUserService userService,
        ILicenceRepository licenceRepo,
        ILogger<StripeWebhookService> logger)
    {
        _settings = settings.Value;
        _licenceService = licenceService;
        _userService = userService;
        _licenceRepo = licenceRepo;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(string body, string signature, CancellationToken ct = default)
    {
        // 1. Verify signature
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(body, signature, _settings.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Webhook signature validation failed: {Message}", ex.Message);
            return false;
        }

        // 2. Idempotency check
        var alreadyProcessed = await _licenceRepo.StripeWebhookEventExistsAsync(stripeEvent.Id, ct);
        if (alreadyProcessed)
        {
            _logger.LogDebug("Webhook event {EventId} already processed, skipping", stripeEvent.Id);
            return true;
        }

        // 3. Store raw event (ReceivedAt must be set explicitly — DB default is SQL Server only)
        var record = new StripeWebhookEvent
        {
            StripeEventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            RawPayload = body,
            ReceivedAt = DateTime.UtcNow
        };
        await _licenceRepo.CreateWebhookEventAsync(record, ct);

        // 4. Route and process
        try
        {
            await RouteEventAsync(stripeEvent, ct);
            record.ProcessedAt = DateTime.UtcNow;
            await _licenceRepo.UpdateWebhookEventAsync(record, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process webhook event {EventId} of type {EventType}", stripeEvent.Id, stripeEvent.Type);
            record.FailedAt = DateTime.UtcNow;
            record.ErrorMessage = ex.ToString();
            await _licenceRepo.UpdateWebhookEventAsync(record, ct);
            throw;
        }

        return true;
    }

    public async Task<List<StripeWebhookEvent>> GetFailedEventsAsync(CancellationToken ct = default)
        => await _licenceRepo.GetFailedWebhookEventsAsync(ct);

    public async Task<bool> RetryEventAsync(Guid webhookEventId, CancellationToken ct = default)
    {
        var record = await _licenceRepo.GetWebhookEventAsync(webhookEventId, ct);
        if (record is null) return false;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ParseEvent(record.RawPayload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse stored webhook event {EventId}", webhookEventId);
            return false;
        }

        try
        {
            await RouteEventAsync(stripeEvent, ct);
            record.ProcessedAt = DateTime.UtcNow;
            record.FailedAt = null;
            record.ErrorMessage = null;
            await _licenceRepo.UpdateWebhookEventAsync(record, ct);
            return true;
        }
        catch (Exception ex)
        {
            record.FailedAt = DateTime.UtcNow;
            record.ErrorMessage = ex.ToString();
            await _licenceRepo.UpdateWebhookEventAsync(record, ct);
            return false;
        }
    }

    private async Task RouteEventAsync(Event e, CancellationToken ct)
    {
        switch (e.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutCompletedAsync((Stripe.Checkout.Session)e.Data.Object, ct);
                break;
            case EventTypes.InvoicePaid:
                await HandleInvoicePaidAsync((Invoice)e.Data.Object, ct);
                break;
            case EventTypes.InvoicePaymentFailed:
                await HandlePaymentFailedAsync((Invoice)e.Data.Object, ct);
                break;
            case EventTypes.CustomerSubscriptionUpdated:
                await HandleSubscriptionUpdatedAsync((StripeSubscription)e.Data.Object, ct);
                break;
            case EventTypes.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeletedAsync((StripeSubscription)e.Data.Object, ct);
                break;
            default:
                _logger.LogDebug("Unhandled Stripe event type: {Type}", e.Type);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(Stripe.Checkout.Session session, CancellationToken ct)
    {
        var email = session.CustomerDetails?.Email ?? session.CustomerEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogError("checkout.session.completed: no email available for session {SessionId}", session.Id);
            return;
        }

        var user = await _userService.RegisterOrGetAsync(email, ct);

        await _licenceService.EnsureStripeCustomerAsync(user.UserId, session.CustomerId, email, ct);

        var isSubscription = session.Mode == "subscription";
        var planType = isSubscription ? "monthly" : "lifetime";

        var subscription = await _licenceService.SyncSubscriptionAsync(
            stripeCustomerId: session.CustomerId,
            stripeSubId: session.SubscriptionId ?? session.Id,
            status: "active",
            userId: user.UserId,
            planType: planType,
            ct: ct);

        var licenceKey = LicenceKeyGenerator.Generate();
        await _licenceService.ProvisionLicenceAsync(
            userId: user.UserId,
            subscriptionId: subscription.SubscriptionId,
            licenceKey: licenceKey,
            licenceType: isSubscription ? "individual" : "lifetime",
            ct: ct);

        _logger.LogInformation("Provisioned licence {LicenceKey} for user {Email}", licenceKey, email);
    }

    private async Task HandleInvoicePaidAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice.SubscriptionId is null) return;

        var subscription = await _licenceRepo.GetSubscriptionByStripeIdAsync(invoice.SubscriptionId, ct);
        if (subscription is null) return;

        subscription.Status = "active";
        subscription.CurrentPeriodStart = invoice.PeriodStart;
        subscription.CurrentPeriodEnd = invoice.PeriodEnd;
        subscription.GracePeriodEnd = null;
        await _licenceRepo.UpdateSubscriptionAsync(subscription, ct);

        var licence = await _licenceRepo.GetActiveLicenceForUserAsync(subscription.UserId, ct);
        if (licence is not null)
        {
            licence.IsActive = true;
            licence.ExpiresAt = invoice.PeriodEnd;
            await _licenceRepo.UpdateAsync(licence, ct);
        }

        _logger.LogInformation("Invoice paid for subscription {SubId}, extended to {End}", invoice.SubscriptionId, invoice.PeriodEnd);
    }

    private async Task HandlePaymentFailedAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice.SubscriptionId is null) return;

        var subscription = await _licenceRepo.GetSubscriptionByStripeIdAsync(invoice.SubscriptionId, ct);
        if (subscription is null) return;

        var gracePeriodEnd = DateTime.UtcNow.Add(DefaultGracePeriod);
        subscription.Status = "past_due";
        subscription.GracePeriodEnd = gracePeriodEnd;
        await _licenceRepo.UpdateSubscriptionAsync(subscription, ct);

        var licence = await _licenceRepo.GetActiveLicenceForUserAsync(subscription.UserId, ct);
        if (licence is not null)
        {
            licence.ExpiresAt = gracePeriodEnd;
            await _licenceRepo.UpdateAsync(licence, ct);
        }

        _logger.LogWarning("Payment failed for subscription {SubId}, grace period until {GraceEnd}", invoice.SubscriptionId, gracePeriodEnd);
    }

    private async Task HandleSubscriptionUpdatedAsync(StripeSubscription stripeSub, CancellationToken ct)
    {
        var subscription = await _licenceRepo.GetSubscriptionByStripeIdAsync(stripeSub.Id, ct);
        if (subscription is null) return;

        subscription.Status = stripeSub.Status;
        subscription.CurrentPeriodStart = stripeSub.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = stripeSub.CurrentPeriodEnd;
        subscription.CancelAtPeriodEnd = stripeSub.CancelAtPeriodEnd;
        subscription.CanceledAt = stripeSub.CanceledAt;
        subscription.TrialEnd = stripeSub.TrialEnd;
        await _licenceRepo.UpdateSubscriptionAsync(subscription, ct);

        _logger.LogInformation("Subscription {SubId} updated to status {Status}", stripeSub.Id, stripeSub.Status);
    }

    private async Task HandleSubscriptionDeletedAsync(StripeSubscription stripeSub, CancellationToken ct)
    {
        var subscription = await _licenceRepo.GetSubscriptionByStripeIdAsync(stripeSub.Id, ct);
        if (subscription is null) return;

        subscription.Status = "canceled";
        subscription.CanceledAt = DateTime.UtcNow;
        await _licenceRepo.UpdateSubscriptionAsync(subscription, ct);

        var licence = await _licenceRepo.GetActiveLicenceForUserAsync(subscription.UserId, ct);
        if (licence is not null)
        {
            licence.IsActive = false;
            licence.ExpiresAt = DateTime.UtcNow;
            await _licenceRepo.UpdateAsync(licence, ct);
        }

        _logger.LogInformation("Subscription {SubId} deleted/canceled", stripeSub.Id);
    }
}
