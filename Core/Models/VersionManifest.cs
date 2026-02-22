namespace UtilityMenuSite.Core.Models;

public class VersionManifest
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; }
    public string? ReleaseNotes { get; set; }
}
