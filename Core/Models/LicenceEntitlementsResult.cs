namespace UtilityMenuSite.Core.Models;

public record LicenceEntitlementsResult
{
    public bool IsValid { get; init; }
    public string LicenceKey { get; init; } = string.Empty;
    public string LicenceType { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
    public List<string> Modules { get; init; } = new();
    public string Signature { get; init; } = string.Empty;
}
