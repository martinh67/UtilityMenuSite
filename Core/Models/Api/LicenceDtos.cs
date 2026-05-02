
namespace UtilityMenuSite.Core.Models.Api;

public record LicenceDetailDto(
    Guid LicenceId,
    string LicenceKey,
    string LicenceType,
    int MaxActivations,
    bool IsActive,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    IReadOnlyList<string> GrantedModules,
    IReadOnlyList<MachineDto> Machines);

public record MachineDto(
    Guid MachineId,
    Guid LicenceId,
    string MachineFingerprint,
    string? MachineName,
    string? OperatingSystem,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    bool IsActive);

public record SubscriptionDto(
    Guid SubscriptionId,
    string Status,
    string PlanType,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    DateTime? GracePeriodEnd,
    DateTime? TrialEnd,
    bool CancelAtPeriodEnd,
    string? StripeCustomerId);

public record GrantModuleRequest(Guid ModuleId, DateTime? ExpiresAt);

public record DashboardSummaryDto(
    ProfileDto Profile,
    LicenceDetailDto? ActiveLicence,
    SubscriptionDto? Subscription,
    string? LatestVersion,
    string? LatestVersionDownloadUrl);
