namespace UtilityMenuSite.Core.Interfaces;

public interface IEmailService
{
    /// <summary>Sends a plain-text + HTML email. Uses the provider configured in EmailSettings.</summary>
    Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken ct = default);

    /// <summary>Sends a welcome email to a newly registered user.</summary>
    Task SendWelcomeAsync(string toAddress, string displayName, CancellationToken ct = default);

    /// <summary>Sends a licence-issued notification including the licence key.</summary>
    Task SendLicenceIssuedAsync(string toAddress, string licenceKey, CancellationToken ct = default);

    /// <summary>Sends a payment-failed warning with a link to update billing details.</summary>
    Task SendPaymentFailedAsync(string toAddress, string billingPortalUrl, CancellationToken ct = default);
}
