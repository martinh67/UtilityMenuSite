using System;
using System.Collections.Generic;
using UtilityMenuSite.Core.Models.Api;

namespace UtilityMenuSite.Services.Api;

/// <summary>
/// Typed HTTP client for UtilityMenuAPI. Single seam between the Blazor UI
/// and the backend — every page that needs server data goes through here.
/// All methods return ApiResult/ApiResult&lt;T&gt; — never throw on expected
/// failures (auth, validation, rate limits, network).
/// </summary>
public interface IUtilityMenuApiClient
{
    // ── Account ──────────────────────────────────────────────────────────────
    Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<ApiResult<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task<ApiResult> LogoutAsync(LogoutRequest request, CancellationToken ct = default);
    Task<ApiResult<UserSummary>> GetMeAsync(CancellationToken ct = default);
    Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<ApiResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);

    // ── Profile ──────────────────────────────────────────────────────────────
    Task<ApiResult<ProfileDto>> GetProfileAsync(CancellationToken ct = default);
    Task<ApiResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<ApiResult<string>> RegenerateApiTokenAsync(CancellationToken ct = default);

    // ── Dashboard / Licence / Subscription ───────────────────────────────────
    Task<ApiResult<DashboardSummaryDto>> GetDashboardAsync(CancellationToken ct = default);
    Task<ApiResult<LicenceDetailDto>> GetActiveLicenceAsync(CancellationToken ct = default);
    Task<ApiResult<SubscriptionDto>> GetSubscriptionAsync(CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<MachineDto>>> GetMachinesAsync(CancellationToken ct = default);
    Task<ApiResult> DeactivateMachineAsync(Guid machineId, CancellationToken ct = default);

    // ── Checkout ─────────────────────────────────────────────────────────────
    Task<ApiResult<CreateCheckoutResponse>> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct = default);
    Task<ApiResult<CheckoutStatusResponse>> GetCheckoutStatusAsync(string sessionId, CancellationToken ct = default);
    Task<ApiResult<BillingPortalResponse>> CreateBillingPortalAsync(BillingPortalRequest request, CancellationToken ct = default);

    // ── Blog (public) ────────────────────────────────────────────────────────
    Task<ApiResult<IReadOnlyList<BlogCategoryDto>>> GetBlogCategoriesAsync(CancellationToken ct = default);
    Task<ApiResult<BlogPostListResponse>> GetPublishedPostsAsync(int skip = 0, int take = 20, CancellationToken ct = default);
    Task<ApiResult<BlogPostDto>> GetPostBySlugAsync(string slug, CancellationToken ct = default);

    // ── Blog (admin) ─────────────────────────────────────────────────────────
    Task<ApiResult<BlogPostListResponse>> GetAllPostsAsync(int skip = 0, int take = 50, CancellationToken ct = default);
    Task<ApiResult<BlogPostDto>> GetPostByIdAsync(Guid postId, CancellationToken ct = default);
    Task<ApiResult<BlogPostDto>> CreatePostAsync(CreateOrUpdateBlogPostRequest request, CancellationToken ct = default);
    Task<ApiResult<BlogPostDto>> UpdatePostAsync(Guid postId, CreateOrUpdateBlogPostRequest request, CancellationToken ct = default);
    Task<ApiResult> DeletePostAsync(Guid postId, CancellationToken ct = default);

    // ── Contact ──────────────────────────────────────────────────────────────
    Task<ApiResult> SubmitContactAsync(SubmitContactRequest request, CancellationToken ct = default);

    // ── Admin ────────────────────────────────────────────────────────────────
    Task<ApiResult<AdminStatsDto>> GetAdminStatsAsync(CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<AdminUserSummaryDto>>> SearchUsersAsync(string query, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<ApiResult<AdminUserDetailDto>> GetAdminUserDetailAsync(Guid userId, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<ContactSubmissionDto>>> GetPendingContactsAsync(CancellationToken ct = default);
    Task<ApiResult> ResolveContactAsync(Guid submissionId, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<FailedWebhookEventDto>>> GetFailedWebhookEventsAsync(CancellationToken ct = default);
    Task<ApiResult> RetryWebhookEventAsync(Guid eventId, CancellationToken ct = default);
    Task<ApiResult> GrantModuleAsync(Guid licenceId, GrantModuleRequest request, CancellationToken ct = default);
    Task<ApiResult> RevokeModuleAsync(Guid licenceId, Guid moduleId, CancellationToken ct = default);

    // ── Version manifest ─────────────────────────────────────────────────────
    Task<ApiResult<VersionManifestDto>> GetVersionManifestAsync(CancellationToken ct = default);

    // ── Usage events (Site server-to-server, x-api-key) ──────────────────────
    Task<ApiResult> RecordUsageEventAsync(RecordUsageEventRequest request, CancellationToken ct = default);
}
