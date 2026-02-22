using UtilityMenuSite.Core.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IVersionManifestService
{
    Task<VersionManifest?> GetLatestAsync(CancellationToken ct = default);
}
