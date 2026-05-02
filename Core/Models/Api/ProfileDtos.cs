namespace UtilityMenuSite.Core.Models.Api;

public record UpdateProfileRequest(
    string? DisplayName,
    string? Organisation,
    string? JobRole,
    string? UsageInterests);

public record ProfileDto(
    string IdentityId,
    System.Guid UserId,
    string Email,
    string? DisplayName,
    string? Organisation,
    string? JobRole,
    string? UsageInterests,
    System.DateTime? ProfileCompletedAt,
    System.DateTime CreatedAt,
    string ApiToken);
