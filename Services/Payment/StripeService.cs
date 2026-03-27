using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Infrastructure.Security;

namespace UtilityMenuSite.Services.Payment;

public class StripeService : IStripeService
{
    private readonly StripeSettings _settings;
    private readonly ILicenceService _licenceService;
    private readonly ILicenceRepository _licenceRepo;
    private readonly IUserService _userService;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        IOptions<StripeSettings> settings,
        ILicenceService licenceService,
        ILicenceRepository licenceRepo,
        IUserService userService,
        ILogger<StripeService> logger)
    {
        _settings = settings.Value;
        _licenceService = licenceService;
        _licenceRepo = licenceRepo;
        _userService = userService;
        _logger = logger;

        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        string priceId,
        string customerEmail,
        string mode,
        string planType = "monthly",
        CancellationToken ct = default)
    {
        var stripeCustomerId = await GetOrCreateStripeCustomerAsync(customerEmail, ct);

        var options = new SessionCreateOptions
        {
            Customer = stripeCustomerId,
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions { Price = priceId, Quantity = 1 }
            ],
            Mode = mode,
            SuccessUrl = $"{_settings.BaseUrl}/checkout/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_settings.BaseUrl}/pricing",
            Metadata = new Dictionary<string, string> { ["plan_type"] = planType }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);

        _logger.LogInformation("Created Stripe checkout session {SessionId} for {Email}", session.Id, customerEmail);

        return new CheckoutSessionResult
        {
            SessionId = session.Id,
            CheckoutUrl = session.Url,
            CustomerId = stripeCustomerId
        };
    }

    public async Task<SessionStatusResult> GetSessionStatusAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        var service = new SessionService();
        var session = await service.GetAsync(sessionId, cancellationToken: ct);

        var licenceKey = await _licenceService
            .GetLicenceKeyForStripeCustomerAsync(session.CustomerId, ct);

        // If the payment is complete but the webhook hasn't provisioned the licence yet,
        // do it eagerly here so the success page doesn't time out waiting.
        if (session.PaymentStatus == "paid" && licenceKey is null)
        {
            _logger.LogInformation(
                "Checkout session {SessionId} is paid but no licence found — provisioning eagerly", sessionId);

            var email = session.CustomerDetails?.Email ?? session.CustomerEmail;
            licenceKey = await EnsureProvisionedAsync(session, email, ct);
        }

        var status = MapStatus(session.PaymentStatus, session.Status);
        _logger.LogDebug("Checkout session {SessionId} status: {Status} (payment: {PaymentStatus})", sessionId, status, session.PaymentStatus);

        return new SessionStatusResult
        {
            SessionId = sessionId,
            Status = status,
            LicenceKey = licenceKey,
            CustomerEmail = session.CustomerDetails?.Email
        };
    }

    private async Task<string?> EnsureProvisionedAsync(
        Stripe.Checkout.Session session, string? email, CancellationToken ct)
    {
        try
        {
            // Prefer resolving the user via the existing StripeCustomer record — this is
            // reliable and bypasses any email case-sensitivity issues between Stripe and the DB.
            var stripeCustomerRecord = await _licenceRepo.GetStripeCustomerByStripeIdAsync(session.CustomerId, ct);
            Data.Models.User user;

            if (stripeCustomerRecord is not null)
            {
                var byId = await _userService.GetByIdAsync(stripeCustomerRecord.UserId, ct);
                if (byId is null)
                {
                    _logger.LogError(
                        "StripeCustomer record for {CustomerId} points to unknown UserId {UserId}",
                        session.CustomerId, stripeCustomerRecord.UserId);
                    return null;
                }
                user = byId;
            }
            else
            {
                // No StripeCustomer record yet — fall back to email lookup/create.
                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogError(
                        "Cannot provision licence: no StripeCustomer record and no email for session {SessionId}",
                        session.Id);
                    return null;
                }
                user = await _userService.RegisterOrGetAsync(email, ct);
                await _licenceService.EnsureStripeCustomerAsync(user.UserId, session.CustomerId, email, ct);
            }

            // Re-check — webhook may have beaten us here
            var existing = await _licenceService.GetLicenceKeyForStripeCustomerAsync(session.CustomerId, ct);
            if (existing is not null)
                return existing;

            var isSubscription = session.Mode == "subscription";
            session.Metadata.TryGetValue("plan_type", out var planType);
            planType = isSubscription ? (planType ?? "monthly") : "lifetime";

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

            _logger.LogInformation(
                "Eagerly provisioned licence {LicenceKey} for user {UserId} via session {SessionId}",
                licenceKey, user.UserId, session.Id);

            return licenceKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eager provisioning failed for session {SessionId}", session.Id);
            return null;
        }
    }

    public async Task<BillingPortalResult> CreateBillingPortalSessionAsync(
        string stripeCustomerId,
        CancellationToken ct = default)
    {
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = $"{_settings.BaseUrl}/dashboard"
        };
        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);

        _logger.LogInformation("Created Stripe billing portal session for customer {CustomerId}", stripeCustomerId);

        return new BillingPortalResult { Url = session.Url };
    }

    public async Task<string?> GetStripeCustomerIdForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var customer = await _licenceRepo.GetStripeCustomerAsync(userId, ct);
        return customer?.StripeCustomerId;
    }

    private async Task<string> GetOrCreateStripeCustomerAsync(string email, CancellationToken ct)
    {
        var customerService = new CustomerService();
        var existingCustomers = await customerService.ListAsync(
            new CustomerListOptions { Email = email, Limit = 1 },
            cancellationToken: ct);

        if (existingCustomers.Any())
        {
            _logger.LogDebug("Found existing Stripe customer for email lookup");
            return existingCustomers.First().Id;
        }

        var newCustomer = await customerService.CreateAsync(
            new CustomerCreateOptions { Email = email },
            cancellationToken: ct);

        _logger.LogInformation("Created new Stripe customer {CustomerId}", newCustomer.Id);
        return newCustomer.Id;
    }

    private static string MapStatus(string? paymentStatus, string? sessionStatus)
    {
        if (paymentStatus == "paid") return "complete";
        if (sessionStatus == "expired") return "expired";
        if (paymentStatus == "unpaid" && sessionStatus == "complete") return "failed";
        return "pending";
    }
}
