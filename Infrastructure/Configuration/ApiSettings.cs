namespace UtilityMenuSite.Infrastructure.Configuration;

/// <summary>
/// Configuration for the UtilityMenuAPI HTTP client. Bound from the "Api"
/// section of appsettings + KV-backed app settings.
/// </summary>
public class ApiSettings
{
    /// <summary>Base URL of UtilityMenuAPI (e.g. https://utilitymenu-uat-api.azurewebsites.net).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Pre-shared service key sent as the <c>x-api-key</c> header on
    /// server-to-server calls (no user context). KV-backed in non-Dev.
    /// </summary>
    public string ServiceKey { get; set; } = string.Empty;
}
