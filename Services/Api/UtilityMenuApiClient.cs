using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UtilityMenuSite.Core.Models.Api;

namespace UtilityMenuSite.Services.Api;

/// <summary>
/// Default <see cref="IUtilityMenuApiClient"/> implementation backed by a
/// typed HttpClient. The client's BaseAddress is set in DI from
/// <c>Api:BaseUrl</c>; the <see cref="ApiAuthHandler"/> DelegatingHandler
/// attaches Authorization Bearer + x-api-key per request.
/// </summary>
public class UtilityMenuApiClient : IUtilityMenuApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<UtilityMenuApiClient> _log;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public UtilityMenuApiClient(HttpClient http, ILogger<UtilityMenuApiClient> log)
    {
        _http = http;
        _log = log;
    }

    // ── Account ──────────────────────────────────────────────────────────────

    public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
        => PostJsonAsync<LoginRequest, AuthResponse>("/api/v1/account/login", request, ct);

    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => PostJsonAsync<RegisterRequest, AuthResponse>("/api/v1/account/register", request, ct);

    public Task<ApiResult<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
        => PostJsonAsync<RefreshRequest, AuthResponse>("/api/v1/account/refresh", request, ct);

    public Task<ApiResult> LogoutAsync(LogoutRequest request, CancellationToken ct = default)
        => PostJsonAsync<LogoutRequest>("/api/v1/account/logout", request, ct);

    public async Task<ApiResult<UserSummary>> GetMeAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync("/api/v1/account/me", ct);
            return await ReadResultAsync<UserSummary>(resp, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "GetMeAsync failed");
            return ApiResult<UserSummary>.Failure("network_error", ex.Message);
        }
    }

    public Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
        => PostJsonAsync<ForgotPasswordRequest>("/api/v1/account/forgot-password", request, ct);

    public Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
        => PostJsonAsync<ResetPasswordRequest>("/api/v1/account/reset-password", request, ct);

    public Task<ApiResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default)
        => PostJsonAsync<ConfirmEmailRequest>("/api/v1/account/confirm-email", request, ct);

    // ── Profile ──────────────────────────────────────────────────────────────

    public Task<ApiResult<ProfileDto>> GetProfileAsync(CancellationToken ct = default)
        => GetAsync<ProfileDto>("/api/v1/profile", ct);

    public Task<ApiResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
        => PutJsonAsync<UpdateProfileRequest>("/api/v1/profile", request, ct);

    public Task<ApiResult<string>> RegenerateApiTokenAsync(CancellationToken ct = default)
        => PostJsonAsync<object, string>("/api/v1/profile/regenerate-api-token", new { }, ct);

    // ── Dashboard / Licence / Subscription ───────────────────────────────────

    public Task<ApiResult<DashboardSummaryDto>> GetDashboardAsync(CancellationToken ct = default)
        => GetAsync<DashboardSummaryDto>("/api/v1/dashboard", ct);

    public Task<ApiResult<LicenceDetailDto>> GetActiveLicenceAsync(CancellationToken ct = default)
        => GetAsync<LicenceDetailDto>("/api/v1/dashboard/licence", ct);

    public Task<ApiResult<SubscriptionDto>> GetSubscriptionAsync(CancellationToken ct = default)
        => GetAsync<SubscriptionDto>("/api/v1/dashboard/subscription", ct);

    public Task<ApiResult<IReadOnlyList<MachineDto>>> GetMachinesAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<MachineDto>>("/api/v1/dashboard/machines", ct);

    public Task<ApiResult> DeactivateMachineAsync(Guid machineId, CancellationToken ct = default)
        => PostJsonAsync<object>($"/api/v1/licence/deactivate/{machineId}", new { }, ct);

    // ── Checkout ─────────────────────────────────────────────────────────────

    public Task<ApiResult<CreateCheckoutResponse>> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct = default)
        => PostJsonAsync<CreateCheckoutRequest, CreateCheckoutResponse>("/api/v1/checkout/create", request, ct);

    public Task<ApiResult<CheckoutStatusResponse>> GetCheckoutStatusAsync(string sessionId, CancellationToken ct = default)
        => GetAsync<CheckoutStatusResponse>($"/api/v1/checkout/status?sessionId={Uri.EscapeDataString(sessionId)}", ct);

    public Task<ApiResult<BillingPortalResponse>> CreateBillingPortalAsync(BillingPortalRequest request, CancellationToken ct = default)
        => PostJsonAsync<BillingPortalRequest, BillingPortalResponse>("/api/v1/checkout/billing-portal", request, ct);

    // ── Blog (public) ────────────────────────────────────────────────────────

    public Task<ApiResult<IReadOnlyList<BlogCategoryDto>>> GetBlogCategoriesAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<BlogCategoryDto>>("/api/v1/blog/categories", ct);

    public Task<ApiResult<BlogPostListResponse>> GetPublishedPostsAsync(int skip = 0, int take = 20, CancellationToken ct = default)
        => GetAsync<BlogPostListResponse>($"/api/v1/blog/posts?skip={skip}&take={take}", ct);

    public Task<ApiResult<BlogPostDto>> GetPostBySlugAsync(string slug, CancellationToken ct = default)
        => GetAsync<BlogPostDto>($"/api/v1/blog/posts/{Uri.EscapeDataString(slug)}", ct);

    // ── Blog (admin) ─────────────────────────────────────────────────────────

    public Task<ApiResult<BlogPostListResponse>> GetAllPostsAsync(int skip = 0, int take = 50, CancellationToken ct = default)
        => GetAsync<BlogPostListResponse>($"/api/v1/admin/blog/posts?skip={skip}&take={take}", ct);

    public Task<ApiResult<BlogPostDto>> GetPostByIdAsync(Guid postId, CancellationToken ct = default)
        => GetAsync<BlogPostDto>($"/api/v1/admin/blog/posts/{postId}", ct);

    public Task<ApiResult<BlogPostDto>> CreatePostAsync(CreateOrUpdateBlogPostRequest request, CancellationToken ct = default)
        => PostJsonAsync<CreateOrUpdateBlogPostRequest, BlogPostDto>("/api/v1/admin/blog/posts", request, ct);

    public Task<ApiResult<BlogPostDto>> UpdatePostAsync(Guid postId, CreateOrUpdateBlogPostRequest request, CancellationToken ct = default)
        => PutJsonAsync<CreateOrUpdateBlogPostRequest, BlogPostDto>($"/api/v1/admin/blog/posts/{postId}", request, ct);

    public Task<ApiResult> DeletePostAsync(Guid postId, CancellationToken ct = default)
        => DeleteAsync($"/api/v1/admin/blog/posts/{postId}", ct);

    // ── Contact ──────────────────────────────────────────────────────────────

    public Task<ApiResult> SubmitContactAsync(SubmitContactRequest request, CancellationToken ct = default)
        => PostJsonAsync<SubmitContactRequest>("/api/v1/contact", request, ct);

    // ── Admin ────────────────────────────────────────────────────────────────

    public Task<ApiResult<AdminStatsDto>> GetAdminStatsAsync(CancellationToken ct = default)
        => GetAsync<AdminStatsDto>("/api/v1/admin/stats", ct);

    public Task<ApiResult<IReadOnlyList<AdminUserSummaryDto>>> SearchUsersAsync(string query, int skip = 0, int take = 20, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<AdminUserSummaryDto>>($"/api/v1/admin/users?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}", ct);

    public Task<ApiResult<AdminUserDetailDto>> GetAdminUserDetailAsync(Guid userId, CancellationToken ct = default)
        => GetAsync<AdminUserDetailDto>($"/api/v1/admin/users/{userId}", ct);

    public Task<ApiResult<IReadOnlyList<ContactSubmissionDto>>> GetPendingContactsAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<ContactSubmissionDto>>("/api/v1/admin/contacts", ct);

    public Task<ApiResult> ResolveContactAsync(Guid submissionId, CancellationToken ct = default)
        => PatchJsonAsync<object>($"/api/v1/admin/contacts/{submissionId}/resolve", new { }, ct);

    public Task<ApiResult<IReadOnlyList<FailedWebhookEventDto>>> GetFailedWebhookEventsAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<FailedWebhookEventDto>>("/api/v1/admin/webhooks/failed", ct);

    public Task<ApiResult> RetryWebhookEventAsync(Guid eventId, CancellationToken ct = default)
        => PostJsonAsync<object>($"/api/v1/admin/webhooks/{eventId}/retry", new { }, ct);

    public Task<ApiResult> GrantModuleAsync(Guid licenceId, GrantModuleRequest request, CancellationToken ct = default)
        => PostJsonAsync<GrantModuleRequest>($"/api/v1/admin/licences/{licenceId}/modules", request, ct);

    public Task<ApiResult> RevokeModuleAsync(Guid licenceId, Guid moduleId, CancellationToken ct = default)
        => DeleteAsync($"/api/v1/admin/licences/{licenceId}/modules/{moduleId}", ct);

    // ── Version manifest ─────────────────────────────────────────────────────

    public Task<ApiResult<VersionManifestDto>> GetVersionManifestAsync(CancellationToken ct = default)
        => GetAsync<VersionManifestDto>("/api/v1/version/manifest", ct);

    // ── Usage events ─────────────────────────────────────────────────────────

    public Task<ApiResult> RecordUsageEventAsync(RecordUsageEventRequest request, CancellationToken ct = default)
        => PostJsonAsync<RecordUsageEventRequest>("/api/v1/usage-events", request, ct);

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(path, ct);
            return await ReadResultAsync<T>(resp, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "GET {Path} failed", path);
            return ApiResult<T>.Failure("network_error", ex.Message);
        }
    }

    private async Task<ApiResult<TResp>> PostJsonAsync<TReq, TResp>(string path, TReq body, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync(path, body, JsonOpts, ct);
            return await ReadResultAsync<TResp>(resp, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "POST {Path} failed", path);
            return ApiResult<TResp>.Failure("network_error", ex.Message);
        }
    }

    private async Task<ApiResult> PostJsonAsync<TReq>(string path, TReq body, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync(path, body, JsonOpts, ct);
            if (resp.IsSuccessStatusCode) return ApiResult.Success();
            var err = await ReadErrorAsync(resp, ct);
            return ApiResult.Failure(err.code, err.message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "POST {Path} failed", path);
            return ApiResult.Failure("network_error", ex.Message);
        }
    }

    private async Task<ApiResult<TResp>> PutJsonAsync<TReq, TResp>(string path, TReq body, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.PutAsJsonAsync(path, body, JsonOpts, ct);
            return await ReadResultAsync<TResp>(resp, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "PUT {Path} failed", path);
            return ApiResult<TResp>.Failure("network_error", ex.Message);
        }
    }

    private async Task<ApiResult> PutJsonAsync<TReq>(string path, TReq body, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.PutAsJsonAsync(path, body, JsonOpts, ct);
            if (resp.IsSuccessStatusCode) return ApiResult.Success();
            var err = await ReadErrorAsync(resp, ct);
            return ApiResult.Failure(err.code, err.message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "PUT {Path} failed", path);
            return ApiResult.Failure("network_error", ex.Message);
        }
    }

    private async Task<ApiResult> PatchJsonAsync<TReq>(string path, TReq body, CancellationToken ct)
    {
        try
        {
            var content = JsonContent.Create(body, options: JsonOpts);
            using var req = new HttpRequestMessage(HttpMethod.Patch, path) { Content = content };
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return ApiResult.Success();
            var err = await ReadErrorAsync(resp, ct);
            return ApiResult.Failure(err.code, err.message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "PATCH {Path} failed", path);
            return ApiResult.Failure("network_error", ex.Message);
        }
    }

    private async Task<ApiResult> DeleteAsync(string path, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.DeleteAsync(path, ct);
            if (resp.IsSuccessStatusCode) return ApiResult.Success();
            var err = await ReadErrorAsync(resp, ct);
            return ApiResult.Failure(err.code, err.message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "DELETE {Path} failed", path);
            return ApiResult.Failure("network_error", ex.Message);
        }
    }

    private async Task<ApiResult<T>> ReadResultAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
        {
            // 204 No Content + non-nullable T → caller mistake; treat as failure.
            if (resp.StatusCode == HttpStatusCode.NoContent)
                return ApiResult<T>.Failure("no_content", "API returned 204 No Content for a request that expected a body.");
            try
            {
                var value = await resp.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
                return value is null
                    ? ApiResult<T>.Failure("empty_body", "API returned an empty body.")
                    : ApiResult<T>.Success(value);
            }
            catch (Exception ex)
            {
                return ApiResult<T>.Failure("deserialise_error", ex.Message);
            }
        }
        var err = await ReadErrorAsync(resp, ct);
        return ApiResult<T>.Failure(err.code, err.message);
    }

    private static async Task<(string code, string message)> ReadErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var error = await resp.Content.ReadFromJsonAsync<AuthErrorResponse>(JsonOpts, ct);
            if (error is not null && !string.IsNullOrEmpty(error.Code))
                return (error.Code, error.Message);
        }
        catch
        {
            // Fall through to status-code-based error.
        }
        return ($"http_{(int)resp.StatusCode}", resp.ReasonPhrase ?? "Unknown error");
    }
}
