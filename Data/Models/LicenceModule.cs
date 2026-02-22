namespace UtilityMenuSite.Data.Models;

public class LicenceModule
{
    public Guid LicenceModuleId { get; set; }
    public Guid LicenceId { get; set; }
    public Guid ModuleId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime GrantedAt { get; set; }

    // Navigation
    public Licence Licence { get; set; } = null!;
    public Module Module { get; set; } = null!;
}
