using System;
using System.Collections.Generic;

namespace UtilityMenuSite.Core.Models.Api;

public record AdminStatsDto(
    int TotalUsers,
    int ActiveLicences,
    int RecentSignups,
    int PendingContactSubmissions,
    int FailedWebhooks,
    decimal MonthlyRevenue);

public record AdminUserSummaryDto(
    Guid UserId,
    string? IdentityId,
    string Email,
    string? DisplayName,
    string? Organisation,
    bool HasActiveLicence,
    string? LicenceType,
    bool IsActive,
    DateTime CreatedAt,
    string? ApiToken);

public record ModuleDto(
    Guid ModuleId,
    string ModuleName,
    string? DisplayName,
    string? Description,
    string Tier,
    int SortOrder,
    bool IsActive);

public record AdminUserDetailDto(
    AdminUserSummaryDto User,
    LicenceDetailDto? Licence,
    SubscriptionDto? Subscription,
    IReadOnlyList<ModuleDto> AllModules,
    IReadOnlyList<string> Roles);

public record ContactSubmissionDto(
    Guid SubmissionId,
    string Name,
    string Email,
    string? Subject,
    string Message,
    string Status,
    DateTime SubmittedAt,
    DateTime? ResolvedAt);

public record FailedWebhookEventDto(
    Guid WebhookEventId,
    string StripeEventId,
    string EventType,
    string? ErrorMessage,
    DateTime ReceivedAt,
    DateTime? FailedAt);

public record SubmitContactRequest(
    string Name,
    string Email,
    string? Subject,
    string Message);
