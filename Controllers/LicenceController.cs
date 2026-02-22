using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Services.Licensing;

namespace UtilityMenuSite.Controllers;

[ApiController]
[Route("api/licence")]
public class LicenceController : ControllerBase
{
    private readonly ILicenceService _licenceService;
    private readonly IUserService _userService;
    private readonly ILogger<LicenceController> _logger;

    public LicenceController(
        ILicenceService licenceService,
        IUserService userService,
        ILogger<LicenceController> logger)
    {
        _licenceService = licenceService;
        _userService = userService;
        _logger = logger;
    }

    /// <summary>GET /api/licence/validate?key=UMENU-... — Fast validity check</summary>
    [HttpGet("validate")]
    [EnableRateLimiting("licence-validate")]
    public async Task<IActionResult> Validate([FromQuery] string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "key is required", code = "VALIDATION_FAILED" });

        var result = await _licenceService.ValidateLicenceAsync(key, ct);

        if (!result.IsValid)
            return Ok(new { isValid = false, reason = result.Reason });

        return Ok(new
        {
            isValid = true,
            licenceType = result.LicenceType,
            expiresAt = result.ExpiresAt
        });
    }

    /// <summary>GET /api/licence/entitlements?key=UMENU-... — Get module entitlements</summary>
    [HttpGet("entitlements")]
    [EnableRateLimiting("licence-validate")]
    public async Task<IActionResult> Entitlements([FromQuery] string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "key is required", code = "VALIDATION_FAILED" });

        var result = await _licenceService.GetEntitlementsAsync(key, ct);
        if (result is null)
            return NotFound(new { error = "Licence not found or inactive", code = "LICENCE_NOT_FOUND" });

        return Ok(new
        {
            isValid = result.IsValid,
            licenceKey = result.LicenceKey,
            licenceType = result.LicenceType,
            expiresAt = result.ExpiresAt,
            modules = result.Modules,
            signature = result.Signature
        });
    }

    /// <summary>POST /api/licence/activate — Activate a machine</summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateMachineRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request", code = "VALIDATION_FAILED" });

        var apiToken = GetBearerToken();
        if (string.IsNullOrWhiteSpace(apiToken))
            return Unauthorized(new { error = "API token required", code = "AUTH_REQUIRED" });

        var user = await _userService.GetByApiTokenAsync(apiToken, ct);
        if (user is null)
            return Unauthorized(new { error = "Invalid API token", code = "AUTH_INVALID" });

        try
        {
            var result = await _licenceService.ActivateMachineAsync(request, ct);
            return Ok(new
            {
                machineId = result.MachineId,
                activatedAt = result.ActivatedAt,
                activeCount = result.ActiveCount,
                maxActivations = result.MaxActivations
            });
        }
        catch (SeatLimitExceededException ex)
        {
            return BadRequest(new { error = ex.Message, code = "SEAT_LIMIT_EXCEEDED" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message, code = "LICENCE_INVALID" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate machine for licence {Key}", request.LicenceKey);
            return StatusCode(500, new { error = "Activation failed", code = "SERVER_ERROR" });
        }
    }

    /// <summary>POST /api/licence/deactivate — Deactivate a machine</summary>
    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate([FromBody] DeactivateMachineRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request", code = "VALIDATION_FAILED" });

        var apiToken = GetBearerToken();
        if (string.IsNullOrWhiteSpace(apiToken))
            return Unauthorized(new { error = "API token required", code = "AUTH_REQUIRED" });

        var user = await _userService.GetByApiTokenAsync(apiToken, ct);
        if (user is null)
            return Unauthorized(new { error = "Invalid API token", code = "AUTH_INVALID" });

        var success = await _licenceService.DeactivateMachineAsync(request.MachineId, ct);
        return Ok(new { success });
    }

    private string? GetBearerToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader["Bearer ".Length..].Trim();
        return null;
    }

}

public class DeactivateMachineRequest
{
    public Guid MachineId { get; set; }
}
