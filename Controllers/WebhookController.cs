using Microsoft.AspNetCore.Mvc;
using UtilityMenuSite.Core.Interfaces;

namespace UtilityMenuSite.Controllers;

[ApiController]
[Route("api/stripe")]
public class WebhookController : ControllerBase
{
    private readonly IStripeWebhookService _webhookService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(IStripeWebhookService webhookService, ILogger<WebhookController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    /// <summary>POST /api/stripe/webhook — Receive Stripe webhook events</summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Webhook received without Stripe-Signature header");
            return BadRequest(new { error = "Missing Stripe-Signature header" });
        }

        try
        {
            var success = await _webhookService.ProcessAsync(body, signature, ct);
            return success ? Ok() : BadRequest(new { error = "Signature verification failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook processing threw an exception — Stripe will retry");
            return StatusCode(500);
        }
    }
}
