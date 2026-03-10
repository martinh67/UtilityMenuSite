namespace UtilityMenuSite.Data.Models;

public class User
{
    public Guid UserId { get; set; }
    /// <summary>
    /// The ASP.NET Core Identity user ID. Null for users created via Stripe checkout who have not
    /// yet registered a site account. Set by <see cref="UserService.RegisterFromIdentityAsync"/>.
    /// </summary>
    public string? IdentityId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Organisation { get; set; }
    public string? JobRole { get; set; }
    public string? UsageInterests { get; set; }
    public DateTime? ProfileCompletedAt { get; set; }
    public string? ExternalId { get; set; }
    public string ApiToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public StripeCustomer? StripeCustomer { get; set; }
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Licence> Licences { get; set; } = new List<Licence>();
    public ICollection<ApiToken> ApiTokens { get; set; } = new List<ApiToken>();
    public ICollection<UsageEvent> UsageEvents { get; set; } = new List<UsageEvent>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
}
