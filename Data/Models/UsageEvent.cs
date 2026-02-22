namespace UtilityMenuSite.Data.Models;

public class UsageEvent
{
    public Guid EventId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? LicenceId { get; set; }
    public Guid? MachineId { get; set; }
    public Guid? ModuleId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? EventData { get; set; }
    public DateTime OccurredAt { get; set; }

    // Navigation
    public User? User { get; set; }
    public Licence? Licence { get; set; }
    public Machine? Machine { get; set; }
    public Module? Module { get; set; }
}
