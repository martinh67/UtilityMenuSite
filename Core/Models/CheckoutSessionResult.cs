namespace UtilityMenuSite.Core.Models;

public record CheckoutSessionResult
{
    public string SessionId { get; init; } = string.Empty;
    public string CheckoutUrl { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
}
