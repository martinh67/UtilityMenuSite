using System.Text.Json;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;

namespace UtilityMenuSite.Services;

public class VersionManifestService : IVersionManifestService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<VersionManifestService> _logger;

    public VersionManifestService(IWebHostEnvironment env, ILogger<VersionManifestService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<VersionManifest?> GetLatestAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(_env.WebRootPath, "downloads", "version.json");
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<VersionManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read version manifest from {Path}", path);
            return null;
        }
    }
}
