using System.ComponentModel.DataAnnotations;

namespace UtilityMenuSite.Core.Models;

public class ContactFormDto
{
    [Required, MinLength(2), MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required, MinLength(20), MaxLength(5000)]
    public string Message { get; set; } = string.Empty;

    // Honeypot - must remain empty
    public string? Website { get; set; }
}
