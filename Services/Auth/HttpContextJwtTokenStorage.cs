using System.Security.Claims;

namespace UtilityMenuSite.Services.Auth;

/// <summary>
/// Token storage backed by claims on the authenticated user. Login/Register
/// pages call <see cref="AuthCookieIssuer.IssueAsync"/> which packs the
/// access + refresh token as claims into the auth cookie. Reads pull straight
/// from <see cref="HttpContext.User"/> claims (synchronous).
///
/// The cookie is encrypted by ASP.NET Data Protection (persisted to blob
/// storage per <see cref="DataProtectionSettings"/> in Program.cs), so the
/// tokens are not visible in the browser.
/// </summary>
public class HttpContextJwtTokenStorage : IJwtTokenStorage
{
    public const string AccessTokenClaim = "umenu.access_token";
    public const string RefreshTokenClaim = "umenu.refresh_token";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextJwtTokenStorage(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetAccessToken()
        => _httpContextAccessor.HttpContext?.User?.FindFirst(AccessTokenClaim)?.Value;

    public string? GetRefreshToken()
        => _httpContextAccessor.HttpContext?.User?.FindFirst(RefreshTokenClaim)?.Value;

    public void SetTokens(string accessToken, string refreshToken, DateTime expiresAt)
        => throw new InvalidOperationException(
            "Use AuthCookieIssuer.IssueAsync from Login/Register pages — token persistence flows through HttpContext.SignInAsync.");

    public void Clear()
        => throw new InvalidOperationException(
            "Use HttpContext.SignOutAsync from Logout — clearing flows through cookie auth.");
}
