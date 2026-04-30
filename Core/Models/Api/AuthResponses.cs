namespace UtilityMenuSite.Core.Models.Api;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserSummary User);

public record UserSummary(
    string IdentityId,
    Guid? UserId,
    string Email,
    string? DisplayName,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    string? LicenceType,
    string? LicenceKey);

public record AuthErrorResponse(string Code, string Message);
