using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Stripe.Checkout;
using Xunit;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Services.Payment;

namespace UtilityMenuSite.Tests.Services;

/// <summary>
/// Unit tests for StripeWebhookService.HandleCheckoutCompletedAsync — the core
/// handler that runs when Stripe reports a completed checkout session.
///
/// Tests call the internal method directly (via InternalsVisibleTo) so they are
/// not coupled to JSON parsing or Stripe signature verification.
/// </summary>
public class StripeWebhookServiceTests
{
    private readonly Mock<ILicenceService>               _licenceServiceMock = new();
    private readonly Mock<IUserService>                  _userServiceMock    = new();
    private readonly Mock<ILicenceRepository>            _licenceRepoMock    = new();
    private readonly Mock<IEmailService>                 _emailServiceMock   = new();
    private readonly Mock<ILogger<StripeWebhookService>> _loggerMock         = new();

    private readonly StripeSettings _stripeSettings = new()
    {
        SecretKey     = "sk_test_placeholder",
        WebhookSecret = "whsec_placeholder",
        BaseUrl       = "https://localhost:5001"
    };

    private readonly LicensingSettings _licensingSettings = new()
    {
        HmacSigningKey      = "dGVzdC1obWFjLWtleS10aGF0LWlzLWxvbmctZW5vdWdoLWZvci1zaGEyNTY=",
        StalenessWindowDays = 7,
        GracePeriodDays     = 7
    };

    private StripeWebhookService CreateSut() => new(
        Options.Create(_stripeSettings),
        Options.Create(_licensingSettings),
        _licenceServiceMock.Object,
        _userServiceMock.Object,
        _licenceRepoMock.Object,
        _emailServiceMock.Object,
        _loggerMock.Object);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Session BuildSession(
        string  email,
        string  customerId,
        string? subscriptionId = "sub_test123",
        string  mode           = "subscription",
        string  planType       = "monthly")
    {
        var session = new Session();

        // Use reflection to set read-only Stripe SDK properties
        typeof(Session).GetProperty(nameof(Session.Id))!
            .SetValue(session, "cs_test_" + Guid.NewGuid().ToString("N"));
        typeof(Session).GetProperty(nameof(Session.Mode))!
            .SetValue(session, mode);
        typeof(Session).GetProperty(nameof(Session.PaymentStatus))!
            .SetValue(session, "paid");
        typeof(Session).GetProperty(nameof(Session.Status))!
            .SetValue(session, "complete");
        typeof(Session).GetProperty(nameof(Session.CustomerId))!
            .SetValue(session, customerId);
        typeof(Session).GetProperty(nameof(Session.SubscriptionId))!
            .SetValue(session, subscriptionId);

        var details = new SessionCustomerDetails();
        typeof(SessionCustomerDetails).GetProperty(nameof(SessionCustomerDetails.Email))!
            .SetValue(details, email);
        typeof(Session).GetProperty(nameof(Session.CustomerDetails))!
            .SetValue(session, details);

        var metadata = new Dictionary<string, string> { ["plan_type"] = planType };
        typeof(Session).GetProperty(nameof(Session.Metadata))!
            .SetValue(session, metadata);

        return session;
    }

    // ── RetryEventAsync — basic guard ─────────────────────────────────────────

    [Fact]
    public async Task RetryEvent_WhenEventNotFound_ReturnsFalse()
    {
        _licenceRepoMock
            .Setup(r => r.GetWebhookEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StripeWebhookEvent?)null);

        var result = await CreateSut().RetryEventAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    // ── HandleCheckoutCompleted — new subscription ────────────────────────────

    [Fact]
    public async Task HandleCheckoutCompleted_WhenNewSubscription_ProvisionesLicenceAndSendsEmail()
    {
        const string email      = "user@example.com";
        const string customerId = "cus_new123";
        const string subId      = "sub_new123";
        var userId              = Guid.NewGuid();
        var subscriptionId      = Guid.NewGuid();

        var session      = BuildSession(email, customerId, subId);
        var user         = new User { UserId = userId, Email = email, ApiToken = "tok" };
        var subscription = new Subscription { SubscriptionId = subscriptionId };

        _userServiceMock
            .Setup(u => u.RegisterOrGetAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _licenceServiceMock
            .Setup(l => l.EnsureStripeCustomerAsync(userId, customerId, email, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _licenceServiceMock
            .Setup(l => l.GetLicenceKeyForStripeCustomerAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _licenceServiceMock
            .Setup(l => l.SyncSubscriptionAsync(customerId, subId, "active", userId, "monthly", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceServiceMock
            .Setup(l => l.ProvisionLicenceAsync(userId, subscriptionId, It.IsAny<string>(), "individual", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Licence { LicenceKey = "UMENU-NEW1-NEW1-NEW1" });
        _emailServiceMock
            .Setup(e => e.SendLicenceIssuedAsync(email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateSut().HandleCheckoutCompletedAsync(session, CancellationToken.None);

        _licenceServiceMock.Verify(
            l => l.ProvisionLicenceAsync(userId, subscriptionId, It.IsAny<string>(), "individual", It.IsAny<CancellationToken>()),
            Times.Once);
        _emailServiceMock.Verify(
            e => e.SendLicenceIssuedAsync(email, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleCheckoutCompleted_WhenLicenceAlreadyExists_SkipsProvisioningAndSendsEmail()
    {
        const string email       = "user@example.com";
        const string customerId  = "cus_exist123";
        const string existingKey = "UMENU-EXIST-EXIST-EX";
        var userId               = Guid.NewGuid();

        var session = BuildSession(email, customerId);
        var user    = new User { UserId = userId, Email = email, ApiToken = "tok" };

        _userServiceMock
            .Setup(u => u.RegisterOrGetAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _licenceServiceMock
            .Setup(l => l.EnsureStripeCustomerAsync(userId, customerId, email, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _licenceServiceMock
            .Setup(l => l.GetLicenceKeyForStripeCustomerAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingKey);
        _emailServiceMock
            .Setup(e => e.SendLicenceIssuedAsync(email, existingKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateSut().HandleCheckoutCompletedAsync(session, CancellationToken.None);

        _licenceServiceMock.Verify(
            l => l.ProvisionLicenceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _emailServiceMock.Verify(
            e => e.SendLicenceIssuedAsync(email, existingKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Missing email ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleCheckoutCompleted_WhenEmailMissing_SkipsAllProvisioning()
    {
        var session = BuildSession(email: "", customerId: "cus_noemail");

        await CreateSut().HandleCheckoutCompletedAsync(session, CancellationToken.None);

        _userServiceMock.Verify(u => u.RegisterOrGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _licenceServiceMock.Verify(
            l => l.ProvisionLicenceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── One-time payment → lifetime licence ───────────────────────────────────

    [Fact]
    public async Task HandleCheckoutCompleted_ForOneTimePayment_ProvisionesLifetimeLicence()
    {
        const string email      = "buyer@example.com";
        const string customerId = "cus_lifetime";
        var userId              = Guid.NewGuid();
        var subscriptionId      = Guid.NewGuid();

        // mode = "payment", subscriptionId = null → session.Id used as stub
        var session      = BuildSession(email, customerId, subscriptionId: null, mode: "payment", planType: "lifetime");
        var user         = new User { UserId = userId, Email = email, ApiToken = "tok" };
        var subscription = new Subscription { SubscriptionId = subscriptionId };

        _userServiceMock
            .Setup(u => u.RegisterOrGetAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _licenceServiceMock
            .Setup(l => l.EnsureStripeCustomerAsync(userId, customerId, email, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _licenceServiceMock
            .Setup(l => l.GetLicenceKeyForStripeCustomerAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _licenceServiceMock
            .Setup(l => l.SyncSubscriptionAsync(customerId, It.IsAny<string>(), "active", userId, "lifetime", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceServiceMock
            .Setup(l => l.ProvisionLicenceAsync(userId, subscriptionId, It.IsAny<string>(), "lifetime", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Licence { LicenceKey = "UMENU-LIFE-LIFE-LIF" });
        _emailServiceMock
            .Setup(e => e.SendLicenceIssuedAsync(email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateSut().HandleCheckoutCompletedAsync(session, CancellationToken.None);

        _licenceServiceMock.Verify(
            l => l.ProvisionLicenceAsync(userId, subscriptionId, It.IsAny<string>(), "lifetime", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
