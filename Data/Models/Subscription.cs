namespace UtilityMenuSite.Data.Models;

public class Subscription
{
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }
    public string StripeCustomerId { get; set; } = string.Empty;
    public string StripeSubscriptionId { get; set; } = string.Empty;
    public string? StripePriceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? GracePeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime? TrialStart { get; set; }
    public DateTime? TrialEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Licence> Licences { get; set; } = new List<Licence>();
}
