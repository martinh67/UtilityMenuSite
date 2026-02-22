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
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IUserService userService,
        IContactService contactService,
        IStripeWebhookService webhookService,
        ILogger<AdminController> logger)
    {
        _userService = userService;
        _contactService = contactService;
        _webhookService = webhookService;
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
}

public class ResolveContactRequest
{
    public string? Notes { get; set; }
}
