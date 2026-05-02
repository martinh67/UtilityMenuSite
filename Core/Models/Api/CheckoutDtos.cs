namespace UtilityMenuSite.Core.Models.Api;

public record CreateCheckoutRequest(
    string PlanType,
    string Email,
    string SuccessUrl,
    string CancelUrl);

public record CreateCheckoutResponse(string SessionId, string CheckoutUrl);

public record CheckoutStatusResponse(
    string Status,
    string PaymentStatus,
    string? LicenceKey,
    string? CustomerEmail);

public record BillingPortalRequest(string ReturnUrl);
public record BillingPortalResponse(string PortalUrl);

public record VersionManifestDto(
    string Version,
    System.DateTime? ReleaseDate,
    string DownloadUrl,
    string? ReleaseNotesUrl,
    string? MinExcelVersion,
    string[]? Changelog);

public record RecordUsageEventRequest(
    string EventType,
    System.Guid? UserId,
    System.Guid? LicenceId,
    string? Metadata);
