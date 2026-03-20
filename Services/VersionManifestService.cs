using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;

namespace UtilityMenuSite.Services;

/// <summary>
/// Fetches the latest UtilityMenu release from the GitHub Releases API and maps it to a
/// <see cref="VersionManifest"/>. Results are cached for 15 minutes to avoid hitting
/// GitHub rate limits. Falls back to <c>wwwroot/downloads/version.json</c> if the API
/// call fails.
/// </summary>
public class VersionManifestService : IVersionManifestService
{
    private const string CacheKey = "github_latest_release";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<VersionManifestService> _logger;

    public VersionManifestService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<VersionManifestService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _env = env;
        _config = config;
        _logger = logger;
    }

    public async Task<VersionManifest?> GetLatestAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out VersionManifest? cached))
            return cached;

        var manifest = await FetchFromGitHubAsync(ct)
                    ?? await ReadFromFileAsync(ct);

        if (manifest is not null)
            _cache.Set(CacheKey, manifest, CacheDuration);

        return manifest;
    }

    // ── GitHub Releases API ───────────────────────────────────────────────────

    private async Task<VersionManifest?> FetchFromGitHubAsync(CancellationToken ct)
    {
        var owner = _config["GitHub:RepoOwner"];
        var repo  = _config["GitHub:RepoName"];

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            _logger.LogWarning("GitHub:RepoOwner or GitHub:RepoName is not configured — skipping GitHub release check");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("github");
            var response = await client.GetAsync($"repos/{owner}/{repo}/releases/latest", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub Releases API returned {StatusCode} for {Owner}/{Repo}",
                    response.StatusCode, owner, repo);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseGitHubRelease(json, owner, repo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch latest release from GitHub for {Owner}/{Repo}", owner, repo);
            return null;
        }
    }

    private VersionManifest? ParseGitHubRelease(string json, string owner, string repo)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(tagName)) return null;

        // Strip leading "v" from tag (e.g. "v1.2.3" → "1.2.3")
        var version = tagName.TrimStart('v');

        // Find the first .exe asset
        string? downloadUrl = null;
        string? fileName = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                {
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    fileName = name;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            _logger.LogWarning("GitHub release {Tag} for {Owner}/{Repo} has no .exe asset", tagName, owner, repo);
            return null;
        }

        string? releaseDate = null;
        if (root.TryGetProperty("published_at", out var pub) && pub.TryGetDateTime(out var dt))
            releaseDate = dt.ToString("yyyy-MM-dd");

        // Build release notes URL pointing to the website blog, falling back to the GitHub release page
        var releaseNotesUrl = $"https://github.com/{owner}/{repo}/releases/tag/{tagName}";

        return new VersionManifest
        {
            Version         = version,
            DownloadUrl     = downloadUrl,
            FileName        = fileName,
            ReleaseDate     = releaseDate,
            ReleaseNotesUrl = releaseNotesUrl
        };
    }

    // ── version.json fallback ────────────────────────────────────────────────

    private async Task<VersionManifest?> ReadFromFileAsync(CancellationToken ct)
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
            _logger.LogError(ex, "Failed to read version manifest fallback from {Path}", path);
            return null;
        }
    }
}
