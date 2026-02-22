using System.ComponentModel.DataAnnotations;

namespace UtilityMenuSite.Core.Models;

public class CreateCheckoutRequest
{
    [Required]
    public string PriceId { get; set; } = string.Empty;

    [Required]
    public string Mode { get; set; } = "subscription";

    [Required, EmailAddress]
    public string CustomerEmail { get; set; } = string.Empty;
}
