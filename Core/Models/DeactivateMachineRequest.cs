namespace UtilityMenuSite.Core.Models;

/// <summary>
/// Request body for POST /api/licence/deactivate.
/// Supports two deactivation paths:
///   - Dashboard path: provide MachineId (Guid) directly.
///   - Add-in path: provide LicenceKey + MachineFingerprint (the add-in does not store MachineId locally).
/// </summary>
public class DeactivateMachineRequest
{
    /// <summary>Dashboard path — deactivate by primary key.</summary>
    public Guid MachineId { get; set; }

    /// <summary>Add-in path — deactivate by licence key + machine fingerprint.</summary>
    public string? LicenceKey { get; set; }

    /// <summary>Add-in path — SHA-256 fingerprint computed by the add-in.</summary>
    public string? MachineFingerprint { get; set; }
}
