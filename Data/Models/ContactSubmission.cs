namespace UtilityMenuSite.Data.Models;

public class ContactSubmission
{
    public Guid SubmissionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? IpAddress { get; set; }
}
