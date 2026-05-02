using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using UtilityMenuSite.Infrastructure.Configuration;

namespace UtilityMenuSite.Services.Auth;

/// <summary>
/// DelegatingHandler attached to the typed UtilityMenuApiClient HttpClient.
/// Attaches:
///   - <c>Authorization: Bearer {accessToken}</c> when the user is authenticated.
///   - <c>x-api-key: {ApiSettings.ServiceKey}</c> always — the API's
///     <c>MultiScheme</c> auth gives precedence to ApiKey, so for
///     server-to-server calls (no user context) the Site is always recognised
///     as the trusted Site service principal even before login.
/// </summary>
public class ApiAuthHandler : DelegatingHandler
{
    private readonly IJwtTokenStorage _tokens;
    private readonly ApiSettings _settings;

    public ApiAuthHandler(IJwtTokenStorage tokens, IOptions<ApiSettings> settings)
    {
        _tokens = tokens;
        _settings = settings.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_settings.ServiceKey))
            request.Headers.TryAddWithoutValidation("x-api-key", _settings.ServiceKey);

        var token = _tokens.GetAccessToken();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
