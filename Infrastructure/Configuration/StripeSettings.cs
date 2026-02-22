namespace UtilityMenuSite.Infrastructure.Configuration;

public class StripeSettings
{
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public StripePrices Prices { get; set; } = new();
}

public class StripePrices
{
    public string ProMonthly { get; set; } = string.Empty;
    public string ProAnnual { get; set; } = string.Empty;
    public string CustomModule { get; set; } = string.Empty;
}
