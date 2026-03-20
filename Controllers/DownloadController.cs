using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityMenuSite.Core.Constants;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Controllers;

[ApiController]
[Route("api/download")]
[Authorize]
public class DownloadController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IVersionManifestService _versionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(
        IUserService userService,
        IVersionManifestService versionService,
        IHttpClientFactory httpClientFactory,
        AppDbContext db,
        ILogger<DownloadController> logger)
    {
        _userService = userService;
        _versionService = versionService;
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Authenticated download endpoint. Records a download event then proxies the
    /// installer binary directly to the client — the user never leaves the site.
    /// </summary>
    [HttpGet("installer")]
    public async Task<IActionResult> GetInstaller(CancellationToken ct)
    {
        var identityId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(identityId))
            return Unauthorized();

        var user = await _userService.GetByIdentityIdAsync(identityId, ct);
        if (user is null)
            return Unauthorized();

        var manifest = await _versionService.GetLatestAsync(ct);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            return NotFound("Installer download is not currently available.");

        var eventData = JsonSerializer.Serialize(new
        {
            version = manifest.Version,
            ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent = Request.Headers.UserAgent.ToString()
        });

        _db.UsageEvents.Add(new UsageEvent
        {
            UserId = user.UserId,
            EventType = EventTypeConstants.InstallerDownload,
            EventData = eventData,
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} downloaded installer v{Version}", user.UserId, manifest.Version);

        // Proxy the file — fetch from upstream and stream directly to the response
        // so the user downloads from this domain without ever being redirected.
        var client = _httpClientFactory.CreateClient("installer-proxy");
        HttpResponseMessage upstream;
        try
        {
            upstream = await client.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch installer from upstream URL {Url}", manifest.DownloadUrl);
            return StatusCode(502, "Could not retrieve the installer. Please try again later.");
        }

        if (!upstream.IsSuccessStatusCode)
        {
            _logger.LogError("Upstream installer URL returned {StatusCode}", upstream.StatusCode);
            return StatusCode(502, "Could not retrieve the installer. Please try again later.");
        }

        var fileName = manifest.FileName ?? $"UtilityMenu-Setup-{manifest.Version}.exe";
        var stream = await upstream.Content.ReadAsStreamAsync(ct);

        // Hint the browser to save the file rather than attempt to open it.
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");

        // Forward Content-Length if known so the browser can show download progress.
        if (upstream.Content.Headers.ContentLength is { } length)
            Response.Headers.Append("Content-Length", length.ToString());

        return File(stream, "application/octet-stream", enableRangeProcessing: false);
    }
}
