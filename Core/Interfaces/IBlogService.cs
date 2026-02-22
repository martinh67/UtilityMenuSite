using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IBlogService
{
    Task<PagedResult<BlogPost>> GetPublishedPostsAsync(string? categorySlug, int page, int pageSize, CancellationToken ct = default);
    Task<BlogPost?> GetPostBySlugAsync(string slug, CancellationToken ct = default);
    Task<BlogPost?> GetPostByIdAsync(Guid postId, CancellationToken ct = default);
    Task<List<BlogPost>> GetLatestPostsAsync(int count, CancellationToken ct = default);
    Task<List<BlogPost>> GetRelatedPostsAsync(Guid categoryId, Guid excludePostId, int count, CancellationToken ct = default);
    Task<List<BlogCategory>> GetCategoriesAsync(CancellationToken ct = default);
    Task<List<BlogPost>> GetAllForAdminAsync(CancellationToken ct = default);
    Task<BlogPost> CreatePostAsync(CreateBlogPostDto dto, CancellationToken ct = default);
    Task<BlogPost> UpdatePostAsync(Guid postId, CreateBlogPostDto dto, CancellationToken ct = default);
    Task DeletePostAsync(Guid postId, CancellationToken ct = default);
    Task PublishPostAsync(Guid postId, CancellationToken ct = default);
}
