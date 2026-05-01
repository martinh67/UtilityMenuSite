using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace UtilityMenuSite.Services.Auth;

/// <summary>
/// Wraps <see cref="HttpContext.SignInAsync"/> + <see cref="HttpContext.SignOutAsync"/>
/// for the cookie auth scheme. Login/Register/Logout pages call this after a
/// successful API response.
/// </summary>
public interface IAuthCookieIssuer
{
    Task IssueAsync(HttpContext httpContext, AuthResponse auth, bool persistent = true);
    Task ClearAsync(HttpContext httpContext);
}

public class AuthCookieIssuer : IAuthCookieIssuer
{
    public async Task IssueAsync(HttpContext httpContext, AuthResponse auth, bool persistent = true)
    {
        var u = auth.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.IdentityId),
            new(ClaimTypes.Name, u.Email),
            new(ClaimTypes.Email, u.Email),
            new("identity_id", u.IdentityId),
            new("email_confirmed", u.EmailConfirmed.ToString().ToLowerInvariant()),
            new(HttpContextJwtTokenStorage.AccessTokenClaim, auth.AccessToken),
            new(HttpContextJwtTokenStorage.RefreshTokenClaim, auth.RefreshToken),
        };

        if (u.UserId.HasValue)
            claims.Add(new Claim("user_id", u.UserId.Value.ToString()));
        if (!string.IsNullOrWhiteSpace(u.DisplayName))
            claims.Add(new Claim("display_name", u.DisplayName));
        if (!string.IsNullOrWhiteSpace(u.LicenceType))
            claims.Add(new Claim("licence_type", u.LicenceType));
        if (!string.IsNullOrWhiteSpace(u.LicenceKey))
            claims.Add(new Claim("licence_key", u.LicenceKey));

        foreach (var role in u.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var props = new AuthenticationProperties
        {
            IsPersistent = persistent,
            // Cookie lifetime mirrors the API's refresh-chain max-age (7d) — once
            // the refresh token can no longer be rolled, the cookie is meaningless.
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
        };

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }

    public Task ClearAsync(HttpContext httpContext)
        => httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
