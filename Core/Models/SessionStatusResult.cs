namespace UtilityMenuSite.Core.Models;

public record SessionStatusResult
{
    public string SessionId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? LicenceKey { get; init; }
    public string? CustomerEmail { get; init; }
}
