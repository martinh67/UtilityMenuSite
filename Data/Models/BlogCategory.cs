namespace UtilityMenuSite.Data.Models;

public class BlogCategory
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    // Navigation
    public ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
}
