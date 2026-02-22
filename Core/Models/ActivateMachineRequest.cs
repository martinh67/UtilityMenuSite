using System.ComponentModel.DataAnnotations;

namespace UtilityMenuSite.Core.Models;

public class ActivateMachineRequest
{
    [Required]
    public string LicenceKey { get; set; } = string.Empty;

    [Required]
    public string MachineFingerprint { get; set; } = string.Empty;

    public string? MachineName { get; set; }
}
