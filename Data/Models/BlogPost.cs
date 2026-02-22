namespace UtilityMenuSite.Data.Models;

public class BlogPost
{
    public Guid PostId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? AuthorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    // Navigation
    public BlogCategory Category { get; set; } = null!;
    public User? Author { get; set; }
}
