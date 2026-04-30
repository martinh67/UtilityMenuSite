using UtilityMenuSite.Core.Models.Api;

namespace UtilityMenuSite.Services.Api;

/// <summary>
/// Typed HTTP client for UtilityMenuAPI. Single seam between the Blazor UI and
/// the backend — every page that needs server data goes through here.
///
/// Auth methods (Login/Register/Refresh/Logout/Me/...) are fully wired in
/// Phase 3a. Non-auth methods (Licence/Admin/Blog/Contact/etc) currently
/// throw NotImplementedException and will be filled in as page-by-page
/// refactor lands them.
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

    // ── Licence / Dashboard / Admin / Blog / Contact / Checkout / Version ────
    // Phase 3 continuation: each page-rewire adds the corresponding method here
    // and implements it in UtilityMenuApiClient. See PHASE3_CONTINUATION.md.
}
