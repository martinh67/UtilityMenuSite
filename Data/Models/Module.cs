namespace UtilityMenuSite.Data.Models;

public class Module
{
    public Guid ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Tier { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<LicenceModule> LicenceModules { get; set; } = new List<LicenceModule>();
    public ICollection<UsageEvent> UsageEvents { get; set; } = new List<UsageEvent>();
}
