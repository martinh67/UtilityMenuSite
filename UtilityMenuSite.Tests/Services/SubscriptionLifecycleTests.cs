using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Stripe;
using Xunit;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Services.Payment;
using AppSubscription   = UtilityMenuSite.Data.Models.Subscription;
using StripeSubscription = Stripe.Subscription;

namespace UtilityMenuSite.Tests.Services;

/// <summary>
/// Tests for the subscription lifecycle handlers in StripeWebhookService:
///   - invoice.paid         → HandleInvoicePaidAsync
///   - invoice.payment_failed → HandlePaymentFailedAsync
///   - customer.subscription.updated → HandleSubscriptionUpdatedAsync
///   - customer.subscription.deleted → HandleSubscriptionDeletedAsync
///
/// These tests verify that licence and subscription state transitions are correct
/// for renewal, payment failure, grace period, cancellation, and re-activation.
/// </summary>
public class SubscriptionLifecycleTests
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

    /// <summary>
    /// Builds a Stripe Invoice using reflection. Uses the legacy top-level subscription
    /// field (Stripe.net SDK mapping) unless <paramref name="useNewApiFormat"/> is true,
    /// in which case the subscription ID is placed inside the raw JObject at
    /// parent.subscription_details.subscription to simulate the newer API payload format.
    /// </summary>
    private static Invoice BuildInvoice(
        string    subscriptionId,
        DateTime? periodEnd        = null,
        bool      useNewApiFormat  = false)
    {
        var invoice = new Invoice();
        var end     = periodEnd ?? DateTime.UtcNow.AddMonths(1);

        if (useNewApiFormat)
        {
            // Simulate Stripe API 2025+: subscription lives under parent.subscription_details
            var raw = JObject.Parse($@"{{
                ""parent"": {{
                    ""subscription_details"": {{
                        ""subscription"": ""{subscriptionId}""
                    }}
                }}
            }}");
            typeof(Invoice).GetProperty("RawJObject")!.SetValue(invoice, raw);
        }
        else
        {
            typeof(Invoice).GetProperty(nameof(Invoice.SubscriptionId))!
                .SetValue(invoice, subscriptionId);
        }

        typeof(Invoice).GetProperty(nameof(Invoice.PeriodStart))!
            .SetValue(invoice, DateTime.UtcNow);
        typeof(Invoice).GetProperty(nameof(Invoice.PeriodEnd))!
            .SetValue(invoice, end);

        return invoice;
    }

    private static StripeSubscription BuildStripeSubscription(
        string    id,
        string    status            = "active",
        bool      cancelAtPeriodEnd = false,
        DateTime? canceledAt        = null,
        DateTime? trialEnd          = null)
    {
        var sub = new StripeSubscription();
        typeof(StripeSubscription).GetProperty(nameof(StripeSubscription.Id))!
            .SetValue(sub, id);
        typeof(StripeSubscription).GetProperty(nameof(StripeSubscription.Status))!
            .SetValue(sub, status);
        typeof(StripeSubscription).GetProperty(nameof(StripeSubscription.CurrentPeriodStart))!
            .SetValue(sub, DateTime.UtcNow);
        typeof(StripeSubscription).GetProperty(nameof(StripeSubscription.CurrentPeriodEnd))!
            .SetValue(sub, DateTime.UtcNow.AddMonths(1));
        typeof(StripeSubscription).GetProperty(nameof(StripeSubscription.CancelAtPeriodEnd))!
            .SetValue(sub, cancelAtPeriodEnd);
        typeof(StripeSubscription).GetProperty(nameof(StripeSubscription.CanceledAt))!
            .SetValue(sub, canceledAt);
        typeof(StripeSubscription).GetProperty(nameof(StripeSubscription.TrialEnd))!
            .SetValue(sub, trialEnd);
        return sub;
    }

    private static AppSubscription MakeSubscription(Guid userId, string stripeSubId = "sub_test")
        => new() { SubscriptionId = Guid.NewGuid(), UserId = userId, StripeSubscriptionId = stripeSubId, Status = "active" };

    private static Licence MakeLicence(Guid userId, bool isActive = true, DateTime? expiresAt = null)
        => new() { LicenceId = Guid.NewGuid(), UserId = userId, IsActive = isActive, ExpiresAt = expiresAt, LicenceKey = "UMENU-TEST-TEST-TEST" };

    // ── invoice.paid — subscription renewal ──────────────────────────────────

    [Fact]
    public async Task InvoicePaid_WhenSubscriptionFound_UpdatesStatusAndExtendsLicence()
    {
        const string stripeSubId = "sub_renew123";
        var userId       = Guid.NewGuid();
        var subscription = MakeSubscription(userId, stripeSubId);
        var licence      = MakeLicence(userId);
        var periodEnd    = DateTime.UtcNow.AddMonths(1);
        var invoice      = BuildInvoice(stripeSubId, periodEnd);

        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceRepoMock.Setup(r => r.GetActiveLicenceForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);

        await CreateSut().HandleInvoicePaidAsync(invoice, CancellationToken.None);

        subscription.Status.Should().Be("active");
        subscription.GracePeriodEnd.Should().BeNull("a successful payment clears the grace period");
        licence.IsActive.Should().BeTrue();
        licence.ExpiresAt.Should().BeCloseTo(periodEnd, TimeSpan.FromSeconds(5));

        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
        _licenceRepoMock.Verify(r => r.UpdateAsync(licence, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvoicePaid_WhenSubscriptionNotFound_DoesNothing()
    {
        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSubscription?)null);

        await CreateSut().HandleInvoicePaidAsync(BuildInvoice("sub_unknown"), CancellationToken.None);

        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(It.IsAny<AppSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _licenceRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Licence>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvoicePaid_UsingNewApiFormat_ExtractsSubscriptionIdFromRawJson()
    {
        // Stripe 2025+ API: SubscriptionId is null on the SDK object;
        // the subscription ID lives in parent.subscription_details.subscription.
        const string stripeSubId = "sub_newformat";
        var userId       = Guid.NewGuid();
        var subscription = MakeSubscription(userId, stripeSubId);
        var invoice      = BuildInvoice(stripeSubId, useNewApiFormat: true);

        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceRepoMock.Setup(r => r.GetActiveLicenceForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Licence?)null);

        await CreateSut().HandleInvoicePaidAsync(invoice, CancellationToken.None);

        // If the handler reached UpdateSubscriptionAsync, it correctly extracted the ID
        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvoicePaid_ReactivatesLicenceThatWasPastDue()
    {
        // A previously failed payment caused IsActive=true but ExpiresAt=past.
        // The next successful payment should extend ExpiresAt into the future.
        const string stripeSubId = "sub_reactivate";
        var userId       = Guid.NewGuid();
        var subscription = MakeSubscription(userId, stripeSubId);
        subscription.Status     = "past_due";
        subscription.GracePeriodEnd = DateTime.UtcNow.AddDays(-1); // grace expired
        var licence      = MakeLicence(userId, expiresAt: DateTime.UtcNow.AddDays(-1));
        var newPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var invoice      = BuildInvoice(stripeSubId, newPeriodEnd);

        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceRepoMock.Setup(r => r.GetActiveLicenceForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);

        await CreateSut().HandleInvoicePaidAsync(invoice, CancellationToken.None);

        subscription.Status.Should().Be("active");
        subscription.GracePeriodEnd.Should().BeNull();
        licence.IsActive.Should().BeTrue();
        licence.ExpiresAt.Should().BeAfter(DateTime.UtcNow, "ExpiresAt must be extended into the future");
    }

    // ── invoice.payment_failed — grace period ─────────────────────────────────

    [Fact]
    public async Task PaymentFailed_WhenSubscriptionFound_SetsGracePeriodAndSendsEmail()
    {
        const string stripeSubId = "sub_fail123";
        var userId       = Guid.NewGuid();
        var subscription = MakeSubscription(userId, stripeSubId);
        var licence      = MakeLicence(userId);
        var user         = new User { UserId = userId, Email = "user@example.com" };
        var invoice      = BuildInvoice(stripeSubId);

        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceRepoMock.Setup(r => r.GetActiveLicenceForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);
        _userServiceMock.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _emailServiceMock
            .Setup(e => e.SendPaymentFailedAsync(user.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateSut().HandlePaymentFailedAsync(invoice, CancellationToken.None);

        subscription.Status.Should().Be("past_due");
        subscription.GracePeriodEnd.Should().NotBeNull();
        subscription.GracePeriodEnd!.Value.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(_licensingSettings.GracePeriodDays), TimeSpan.FromSeconds(5));
        licence.ExpiresAt.Should().BeCloseTo(subscription.GracePeriodEnd!.Value, TimeSpan.FromSeconds(5));

        _emailServiceMock.Verify(
            e => e.SendPaymentFailedAsync(user.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PaymentFailed_WhenSubscriptionNotFound_DoesNothing()
    {
        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSubscription?)null);

        await CreateSut().HandlePaymentFailedAsync(BuildInvoice("sub_unknown"), CancellationToken.None);

        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(It.IsAny<AppSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailServiceMock.Verify(e => e.SendPaymentFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentFailed_WhenUserNotFound_DoesNotSendEmail()
    {
        // Subscription exists but we can't look up the user — do not crash, do not send email.
        const string stripeSubId = "sub_nouser";
        var userId       = Guid.NewGuid();
        var subscription = MakeSubscription(userId, stripeSubId);
        var invoice      = BuildInvoice(stripeSubId);

        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceRepoMock.Setup(r => r.GetActiveLicenceForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Licence?)null);
        _userServiceMock.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await CreateSut().HandlePaymentFailedAsync(invoice, CancellationToken.None);

        _emailServiceMock.Verify(
            e => e.SendPaymentFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── customer.subscription.deleted — cancellation ──────────────────────────

    [Fact]
    public async Task SubscriptionDeleted_WhenSubscriptionFound_CancelsAndDeactivatesLicence()
    {
        const string stripeSubId = "sub_cancel123";
        var userId       = Guid.NewGuid();
        var subscription = MakeSubscription(userId, stripeSubId);
        var licence      = MakeLicence(userId);
        var stripeSub    = BuildStripeSubscription(stripeSubId, status: "canceled");

        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceRepoMock.Setup(r => r.GetActiveLicenceForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);

        await CreateSut().HandleSubscriptionDeletedAsync(stripeSub, CancellationToken.None);

        subscription.Status.Should().Be("canceled");
        subscription.CanceledAt.Should().NotBeNull();
        licence.IsActive.Should().BeFalse("deleting a subscription must deactivate the licence");
        licence.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
        _licenceRepoMock.Verify(r => r.UpdateAsync(licence, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubscriptionDeleted_WhenNoLicenceExists_StillCancelsSubscription()
    {
        const string stripeSubId = "sub_nolicence";
        var userId       = Guid.NewGuid();
        var subscription = MakeSubscription(userId, stripeSubId);
        var stripeSub    = BuildStripeSubscription(stripeSubId, status: "canceled");

        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _licenceRepoMock.Setup(r => r.GetActiveLicenceForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Licence?)null);

        await CreateSut().HandleSubscriptionDeletedAsync(stripeSub, CancellationToken.None);

        subscription.Status.Should().Be("canceled");
        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
        _licenceRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Licence>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionDeleted_WhenSubscriptionNotFound_DoesNothing()
    {
        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSubscription?)null);

        await CreateSut().HandleSubscriptionDeletedAsync(
            BuildStripeSubscription("sub_ghost", "canceled"), CancellationToken.None);

        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(It.IsAny<AppSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── customer.subscription.updated — status sync ───────────────────────────

    [Fact]
    public async Task SubscriptionUpdated_WhenSubscriptionFound_SyncsAllFields()
    {
        const string stripeSubId    = "sub_update123";
        var userId                  = Guid.NewGuid();
        var subscription            = MakeSubscription(userId, stripeSubId);
        var trialEnd                = DateTime.UtcNow.AddDays(7);
        var stripeSub               = BuildStripeSubscription(stripeSubId, status: "trialing",
                                        cancelAtPeriodEnd: true, trialEnd: trialEnd);

        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(stripeSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await CreateSut().HandleSubscriptionUpdatedAsync(stripeSub, CancellationToken.None);

        subscription.Status.Should().Be("trialing");
        subscription.CancelAtPeriodEnd.Should().BeTrue();
        subscription.TrialEnd.Should().BeCloseTo(trialEnd, TimeSpan.FromSeconds(5));

        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubscriptionUpdated_WhenSubscriptionNotFound_DoesNothing()
    {
        _licenceRepoMock.Setup(r => r.GetSubscriptionByStripeIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSubscription?)null);

        await CreateSut().HandleSubscriptionUpdatedAsync(
            BuildStripeSubscription("sub_ghost"), CancellationToken.None);

        _licenceRepoMock.Verify(r => r.UpdateSubscriptionAsync(It.IsAny<AppSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
