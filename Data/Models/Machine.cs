namespace UtilityMenuSite.Data.Models;

public class Machine
{
    public Guid MachineId { get; set; }
    public Guid LicenceId { get; set; }
    public string MachineFingerprint { get; set; } = string.Empty;
    public string? MachineName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }

    // Navigation
    public Licence Licence { get; set; } = null!;
    public ICollection<UsageEvent> UsageEvents { get; set; } = new List<UsageEvent>();
}
