namespace UtilityMenuSite.Data.Models;

public class StripeCustomer
{
    public string StripeCustomerId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
