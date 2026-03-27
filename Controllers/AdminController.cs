using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityMenuSite.Core.Interfaces;

namespace UtilityMenuSite.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IContactService _contactService;
    private readonly IStripeWebhookService _webhookService;
    private readonly ILicenceService _licenceService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IUserService userService,
        IContactService contactService,
        IStripeWebhookService webhookService,
        ILicenceService licenceService,
        ILogger<AdminController> logger)
    {
        _userService = userService;
        _contactService = contactService;
        _webhookService = webhookService;
        _licenceService = licenceService;
        _logger = logger;
    }

    /// <summary>GET /api/admin/users?q= — Search users</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string q = "", CancellationToken ct = default)
    {
        var users = await _userService.SearchUsersAsync(q, 0, 50, ct);
        return Ok(users.Select(u => new
        {
            u.UserId, u.Email, u.DisplayName, u.IsActive, u.CreatedAt
        }));
    }

    /// <summary>GET /api/admin/users/{id} — User detail</summary>
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetWithLicenceAsync(id, ct);
        if (user is null) return NotFound(new { error = "User not found", code = "NOT_FOUND" });
        return Ok(user);
    }

    /// <summary>GET /api/admin/contacts — Pending contact submissions</summary>
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts(CancellationToken ct)
    {
        var submissions = await _contactService.GetPendingSubmissionsAsync(ct);
        return Ok(submissions);
    }

    /// <summary>PATCH /api/admin/contacts/{id}/resolve — Mark resolved</summary>
    [HttpPatch("contacts/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveContact(Guid id, [FromBody] ResolveContactRequest? request, CancellationToken ct)
    {
        var success = await _contactService.ResolveAsync(id, request?.Notes, ct);
        if (!success) return NotFound(new { error = "Submission not found", code = "NOT_FOUND" });
        return Ok(new { success = true });
    }

    /// <summary>POST /api/admin/webhooks/{id}/retry — Retry failed webhook</summary>
    [HttpPost("webhooks/{id:guid}/retry")]
    public async Task<IActionResult> RetryWebhook(Guid id, CancellationToken ct)
    {
        var success = await _webhookService.RetryEventAsync(id, ct);
        if (!success) return BadRequest(new { error = "Retry failed", code = "RETRY_FAILED" });
        return Ok(new { success = true });
    }

    /// <summary>GET /api/admin/stats — Dashboard stats</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _userService.GetAdminStatsAsync(ct);
        return Ok(stats);
    }

    /// <summary>POST /api/admin/licences/{licenceId}/modules — Grant a module to a licence</summary>
    [HttpPost("licences/{licenceId:guid}/modules")]
    public async Task<IActionResult> GrantModule(Guid licenceId, [FromBody] GrantModuleRequest request, CancellationToken ct)
    {
        await _licenceService.GrantModuleAsync(licenceId, request.ModuleId, request.ExpiresAt, ct);
        _logger.LogInformation("Admin granted module {ModuleId} to licence {LicenceId}", request.ModuleId, licenceId);
        return Ok(new { success = true });
    }

    /// <summary>DELETE /api/admin/licences/{licenceId}/modules/{moduleId} — Revoke a module from a licence</summary>
    [HttpDelete("licences/{licenceId:guid}/modules/{moduleId:guid}")]
    public async Task<IActionResult> RevokeModule(Guid licenceId, Guid moduleId, CancellationToken ct)
    {
        await _licenceService.RevokeModuleAsync(licenceId, moduleId, ct);
        _logger.LogInformation("Admin revoked module {ModuleId} from licence {LicenceId}", moduleId, licenceId);
        return Ok(new { success = true });
    }
}

public class ResolveContactRequest
{
    public string? Notes { get; set; }
}

public class GrantModuleRequest
{
    public Guid ModuleId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
