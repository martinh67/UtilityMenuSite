namespace UtilityMenuSite.Core.Models;

public record ActivateMachineResult
{
    public Guid MachineId { get; init; }
    public DateTime ActivatedAt { get; init; }
    public int ActiveCount { get; init; }
    public int MaxActivations { get; init; }
}
