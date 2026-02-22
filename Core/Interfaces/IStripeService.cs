using UtilityMenuSite.Core.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IStripeService
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(string priceId, string customerEmail, string mode, CancellationToken ct = default);
    Task<SessionStatusResult> GetSessionStatusAsync(string sessionId, CancellationToken ct = default);
    Task<BillingPortalResult> CreateBillingPortalSessionAsync(string stripeCustomerId, CancellationToken ct = default);
}
