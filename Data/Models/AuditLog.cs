namespace UtilityMenuSite.Data.Models;

public class AuditLog
{
    public Guid AuditLogId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime OccurredAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
