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

    // ── Helpers ─────────────────────────────────────────────────────────────

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
