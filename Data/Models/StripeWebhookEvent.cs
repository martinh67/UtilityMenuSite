namespace UtilityMenuSite.Data.Models;

public class StripeWebhookEvent
{
    public Guid WebhookEventId { get; set; }
    public string StripeEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAt { get; set; }
}
