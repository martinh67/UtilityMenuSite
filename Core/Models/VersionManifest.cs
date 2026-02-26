using System.Text.Json.Serialization;

namespace UtilityMenuSite.Core.Models;

/// <summary>
/// Represents the contents of wwwroot/downloads/version.json.
/// Field names match the JSON file exactly (camelCase via System.Text.Json defaults).
/// </summary>
public class VersionManifest
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Optional installer filename (e.g. "UtilityMenu-Setup-1.0.0.exe").</summary>
    public string? FileName { get; set; }

    /// <summary>ISO 8601 release date string, e.g. "2026-02-22". Matches JSON field "releaseDate".</summary>
    public string? ReleaseDate { get; set; }

    /// <summary>SHA-256 hex digest of the installer binary for integrity verification.</summary>
    public string? Sha256 { get; set; }

    /// <summary>URL to the release notes blog post or page.</summary>
    public string? ReleaseNotesUrl { get; set; }

    /// <summary>Minimum supported Excel version string, e.g. "2016".</summary>
    public string? MinExcelVersion { get; set; }

    /// <summary>Short bullet-point changelog entries for this release.</summary>
    public List<string>? Changelog { get; set; }
}
