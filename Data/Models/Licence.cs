namespace UtilityMenuSite.Data.Models;

public class Licence
{
    public Guid LicenceId { get; set; }
    public Guid UserId { get; set; }
    public Guid SubscriptionId { get; set; }
    public string LicenceKey { get; set; } = string.Empty;
    public string LicenceType { get; set; } = string.Empty;
    public int MaxActivations { get; set; } = 2;
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public string? Signature { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
    public ICollection<LicenceModule> LicenceModules { get; set; } = new List<LicenceModule>();
    public ICollection<Machine> Machines { get; set; } = new List<Machine>();
    public ICollection<UsageEvent> UsageEvents { get; set; } = new List<UsageEvent>();
}
