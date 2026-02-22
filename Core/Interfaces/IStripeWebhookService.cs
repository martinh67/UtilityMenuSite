using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IStripeWebhookService
{
    Task<bool> ProcessAsync(string body, string signature, CancellationToken ct = default);
    Task<List<StripeWebhookEvent>> GetFailedEventsAsync(CancellationToken ct = default);
    Task<bool> RetryEventAsync(Guid webhookEventId, CancellationToken ct = default);
}
