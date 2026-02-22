using System.ComponentModel.DataAnnotations;

namespace UtilityMenuSite.Core.Models;

public class CreateBlogPostDto
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    public Guid? AuthorId { get; set; }

    [Required, MaxLength(500)]
    public string Summary { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
}
