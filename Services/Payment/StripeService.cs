using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Infrastructure.Configuration;

namespace UtilityMenuSite.Services.Payment;

public class StripeService : IStripeService
{
    private readonly StripeSettings _settings;
    private readonly ILicenceService _licenceService;
    private readonly ILicenceRepository _licenceRepo;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        IOptions<StripeSettings> settings,
        ILicenceService licenceService,
        ILicenceRepository licenceRepo,
        ILogger<StripeService> logger)
    {
        _settings = settings.Value;
        _licenceService = licenceService;
        _licenceRepo = licenceRepo;
        _logger = logger;

        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        string priceId,
        string customerEmail,
        string mode,
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
            CancelUrl = $"{_settings.BaseUrl}/pricing"
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

        return new SessionStatusResult
        {
            SessionId = sessionId,
            Status = MapStatus(session.PaymentStatus, session.Status),
            LicenceKey = licenceKey,
            CustomerEmail = session.CustomerDetails?.Email
        };
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
            return existingCustomers.First().Id;

        var newCustomer = await customerService.CreateAsync(
            new CustomerCreateOptions { Email = email },
            cancellationToken: ct);

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
