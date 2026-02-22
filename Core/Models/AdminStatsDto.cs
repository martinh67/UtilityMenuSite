namespace UtilityMenuSite.Core.Models;

public record AdminStatsDto
{
    public int TotalUsers { get; init; }
    public int ActiveLicences { get; init; }
    public int RecentSignups { get; init; }
    public int PendingContactSubmissions { get; init; }
    public int FailedWebhookEvents { get; init; }
}
