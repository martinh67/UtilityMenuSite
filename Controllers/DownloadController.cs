using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityMenuSite.Services.Api;

namespace UtilityMenuSite.Controllers;

/// <summary>
/// Site-side proxy for the authenticated installer download. The user hits
/// /api/download/installer on the Site domain (so the browser stays on the
/// Site origin and the auth cookie travels), and we stream the response from
/// UtilityMenuAPI's /api/v1/download/installer through to the client.
///
/// The API endpoint enforces auth, records the UsageEvent, and fetches from
/// GitHub Releases — we deliberately keep that behaviour on the API side and
/// just relay the bytes here.
/// </summary>
[ApiController]
[Route("api/download")]
[Authorize]
public class DownloadController : ControllerBase
{
    private readonly IUtilityMenuApiClient _api;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(IUtilityMenuApiClient api, ILogger<DownloadController> logger)
    {
        _api = api;
        _logger = logger;
    }

    [HttpGet("installer")]
    public async Task<IActionResult> GetInstaller(CancellationToken ct)
    {
        HttpResponseMessage upstream;
        try
        {
            upstream = await _api.GetInstallerAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call API installer endpoint");
            return StatusCode(502, "Could not retrieve the installer. Please try again later.");
        }

        try
        {
            if (!upstream.IsSuccessStatusCode)
            {
                _logger.LogWarning("API installer endpoint returned {StatusCode}", upstream.StatusCode);
                return StatusCode((int)upstream.StatusCode, "Could not retrieve the installer.");
            }

            if (upstream.Content.Headers.ContentDisposition is { } disp)
                Response.Headers.Append("Content-Disposition", disp.ToString());

            if (upstream.Content.Headers.ContentLength is { } length)
                Response.Headers.Append("Content-Length", length.ToString());

            Response.ContentType = "application/octet-stream";
            await upstream.Content.CopyToAsync(Response.Body, ct);
            return new EmptyResult();
        }
        finally
        {
            upstream.Dispose();
        }
    }
}
