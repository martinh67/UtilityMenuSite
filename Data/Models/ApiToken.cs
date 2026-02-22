namespace UtilityMenuSite.Data.Models;

public class ApiToken
{
    public Guid TokenId { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = "Add-in Token";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
