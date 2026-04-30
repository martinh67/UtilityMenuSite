namespace UtilityMenuSite.Services.Auth;

/// <summary>
/// Abstraction over the access + refresh token store used by the Site to
/// authenticate against UtilityMenuAPI. Scoped per Blazor circuit so each
/// connected user reads their own tokens.
/// </summary>
public interface IJwtTokenStorage
{
    /// <summary>Returns the cached access token if available + not expired.</summary>
    string? GetAccessToken();

    /// <summary>Returns the cached refresh token, regardless of access-token validity.</summary>
    string? GetRefreshToken();

    /// <summary>Stores a new token pair and resets the in-memory cache.</summary>
    void SetTokens(string accessToken, string refreshToken, DateTime expiresAt);

    /// <summary>Clears all tokens.</summary>
    void Clear();
}
