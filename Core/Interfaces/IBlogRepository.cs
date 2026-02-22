using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IBlogRepository
{
    Task<List<BlogPost>> GetPublishedAsync(string? categorySlug, int skip, int take, CancellationToken ct = default);
    Task<int> GetPublishedCountAsync(string? categorySlug, CancellationToken ct = default);
    Task<BlogPost?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<BlogPost?> GetByIdAsync(Guid postId, CancellationToken ct = default);
    Task<List<BlogPost>> GetRelatedAsync(Guid categoryId, Guid excludePostId, int count, CancellationToken ct = default);
    Task<List<BlogPost>> GetLatestAsync(int count, CancellationToken ct = default);
    Task<List<BlogPost>> GetAllForAdminAsync(CancellationToken ct = default);
    Task<List<BlogCategory>> GetCategoriesAsync(CancellationToken ct = default);
    Task<BlogPost> CreateAsync(BlogPost post, CancellationToken ct = default);
    Task UpdateAsync(BlogPost post, CancellationToken ct = default);
    Task DeleteAsync(Guid postId, CancellationToken ct = default);
}
