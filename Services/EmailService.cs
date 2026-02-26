using Microsoft.Extensions.Options;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Infrastructure.Configuration;

namespace UtilityMenuSite.Services;

/// <summary>
/// Email service that routes to a provider based on <see cref="EmailSettings.Provider"/>:
/// - "Console"  — writes to the logger (development default, no external calls)
/// - "SendGrid" — sends via the SendGrid HTTP API
/// Additional providers can be added by extending the switch in <see cref="SendAsync"/>.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailService(
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken ct = default)
    {
        switch (_settings.Provider)
        {
            case "SendGrid":
                await SendViaSendGridAsync(toAddress, toName, subject, htmlBody, plainTextBody, ct);
                break;
            default:
                // Console provider — log the email; safe for development environments.
                _logger.LogInformation(
                    "[EMAIL] To={To} Subject={Subject}\n{Body}",
                    toAddress, subject, plainTextBody ?? htmlBody);
                break;
        }
    }

    public Task SendWelcomeAsync(string toAddress, string displayName, CancellationToken ct = default)
    {
        var html = $"""
            <h2>Welcome to UtilityMenu, {System.Net.WebUtility.HtmlEncode(displayName)}!</h2>
            <p>Your account is ready. Visit your <a href="https://utilitymenu.com/dashboard">dashboard</a>
            to view your licence key and download the installer.</p>
            """;
        return SendAsync(toAddress, displayName, "Welcome to UtilityMenu", html, ct: ct);
    }

    public Task SendLicenceIssuedAsync(string toAddress, string licenceKey, CancellationToken ct = default)
    {
        var html = $"""
            <h2>Your UtilityMenu licence is ready</h2>
            <p>Your licence key: <strong>{System.Net.WebUtility.HtmlEncode(licenceKey)}</strong></p>
            <p>Open UtilityMenu in Excel and enter this key when prompted, or visit your
            <a href="https://utilitymenu.com/dashboard">dashboard</a> to copy it.</p>
            """;
        return SendAsync(toAddress, toAddress, "Your UtilityMenu licence key", html, ct: ct);
    }

    public Task SendPaymentFailedAsync(string toAddress, string billingPortalUrl, CancellationToken ct = default)
    {
        var html = $"""
            <h2>Action required: payment failed</h2>
            <p>We were unable to process your last payment. Your access will continue for a short
            grace period while Stripe retries the charge.</p>
            <p><a href="{System.Net.WebUtility.HtmlEncode(billingPortalUrl)}">Update your payment details</a>
            to avoid losing access.</p>
            """;
        return SendAsync(toAddress, toAddress, "UtilityMenu — payment action required", html, ct: ct);
    }

    private async Task SendViaSendGridAsync(
        string toAddress, string toName, string subject,
        string htmlBody, string? plainText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("SendGrid API key is not configured — email not sent to {To}", toAddress);
            return;
        }

        var payload = new
        {
            personalizations = new[]
            {
                new { to = new[] { new { email = toAddress, name = toName } } }
            },
            from = new { email = _settings.FromAddress, name = _settings.FromName },
            subject,
            content = new object[]
            {
                new { type = "text/plain", value = plainText ?? System.Text.RegularExpressions.Regex.Replace(htmlBody, "<[^>]+>", "") },
                new { type = "text/html", value = htmlBody }
            }
        };

        var client = _httpClientFactory.CreateClient("sendgrid");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        var response = await client.PostAsJsonAsync("https://api.sendgrid.com/v3/mail/send", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SendGrid returned {Status} for email to {To}: {Body}",
                response.StatusCode, toAddress, body);
        }
        else
        {
            _logger.LogInformation("Email sent to {To} via SendGrid: {Subject}", toAddress, subject);
        }
    }
}
