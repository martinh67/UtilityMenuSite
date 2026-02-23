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
    private readonly AppDbContext _db;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(
        IUserService userService,
        IVersionManifestService versionService,
        AppDbContext db,
        ILogger<DownloadController> logger)
    {
        _userService = userService;
        _versionService = versionService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Authenticated download endpoint. Records a download event then redirects to the
    /// actual installer URL from the version manifest.
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

        return Redirect(manifest.DownloadUrl);
    }
}
