using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;

namespace UtilityMenuSite.Controllers;

[ApiController]
[Route("api/checkout")]
public class CheckoutController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly IUserService _userService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        IStripeService stripeService,
        IUserService userService,
        ILogger<CheckoutController> logger)
    {
        _stripeService = stripeService;
        _userService = userService;
        _logger = logger;
    }

    /// <summary>POST /api/checkout/create — Create Stripe Checkout session</summary>
    [HttpPost("create")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCheckoutRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request", code = "VALIDATION_FAILED" });

        // Validate API token from Bearer header
        var apiToken = GetBearerToken();
        if (string.IsNullOrWhiteSpace(apiToken))
            return Unauthorized(new { error = "API token required", code = "AUTH_REQUIRED" });

        var user = await _userService.GetByEmailAsync(request.CustomerEmail, ct);
        if (user is null || user.ApiToken != apiToken)
            return Unauthorized(new { error = "Invalid API token", code = "AUTH_INVALID" });

        if (request.Mode != "subscription" && request.Mode != "payment")
            return BadRequest(new { error = "Mode must be 'subscription' or 'payment'", code = "INVALID_MODE" });

        try
        {
            var result = await _stripeService.CreateCheckoutSessionAsync(
                request.PriceId, request.CustomerEmail, request.Mode, ct);

            return Ok(new { url = result.CheckoutUrl, sessionId = result.SessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkout session for {Email}", request.CustomerEmail);
            return StatusCode(500, new { error = "Failed to create checkout session", code = "STRIPE_ERROR" });
        }
    }

    /// <summary>GET /api/checkout/status?sessionId=... — Poll session status</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { error = "sessionId is required", code = "VALIDATION_FAILED" });

        var apiToken = GetBearerToken();
        if (string.IsNullOrWhiteSpace(apiToken))
            return Unauthorized(new { error = "API token required", code = "AUTH_REQUIRED" });

        try
        {
            var result = await _stripeService.GetSessionStatusAsync(sessionId, ct);
            return Ok(new
            {
                status = result.Status,
                licenceKey = result.LicenceKey,
                customerEmail = result.CustomerEmail
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get checkout status for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to get session status", code = "STRIPE_ERROR" });
        }
    }

    /// <summary>POST /api/checkout/billing-portal — Create billing portal session</summary>
    [HttpPost("billing-portal")]
    public async Task<IActionResult> BillingPortal([FromBody] BillingPortalRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request", code = "VALIDATION_FAILED" });

        var apiToken = GetBearerToken();
        if (string.IsNullOrWhiteSpace(apiToken))
            return Unauthorized(new { error = "API token required", code = "AUTH_REQUIRED" });

        try
        {
            var result = await _stripeService.CreateBillingPortalSessionAsync(request.StripeCustomerId, ct);
            return Ok(new { url = result.Url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create billing portal session");
            return StatusCode(500, new { error = "Failed to create billing portal session", code = "STRIPE_ERROR" });
        }
    }

    private string? GetBearerToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader["Bearer ".Length..].Trim();
        return null;
    }
}

public class BillingPortalRequest
{
    public string StripeCustomerId { get; set; } = string.Empty;
}
