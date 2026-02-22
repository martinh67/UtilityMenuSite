namespace UtilityMenuSite.Core.Models;

public record LicenceValidationResult
{
    public bool IsValid { get; init; }
    public string? LicenceType { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? Reason { get; init; }
}
