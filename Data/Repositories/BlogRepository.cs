using Microsoft.EntityFrameworkCore;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Repositories;

public class BlogRepository : IBlogRepository
{
    private readonly AppDbContext _db;

    public BlogRepository(AppDbContext db) => _db = db;

    public async Task<List<BlogPost>> GetPublishedAsync(string? categorySlug, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.BlogPosts
            .Include(p => p.Category)
            .Include(p => p.Author)
            .Where(p => p.IsPublished);

        if (!string.IsNullOrWhiteSpace(categorySlug))
            query = query.Where(p => p.Category.Slug == categorySlug);

        return await query
            .OrderByDescending(p => p.PublishedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetPublishedCountAsync(string? categorySlug, CancellationToken ct = default)
    {
        var query = _db.BlogPosts.Where(p => p.IsPublished);
        if (!string.IsNullOrWhiteSpace(categorySlug))
            query = query.Where(p => p.Category.Slug == categorySlug);
        return await query.CountAsync(ct);
    }

    public async Task<BlogPost?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _db.BlogPosts
            .Include(p => p.Category)
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);

    public async Task<BlogPost?> GetByIdAsync(Guid postId, CancellationToken ct = default)
        => await _db.BlogPosts
            .Include(p => p.Category)
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.PostId == postId, ct);

    public async Task<List<BlogPost>> GetRelatedAsync(Guid categoryId, Guid excludePostId, int count, CancellationToken ct = default)
        => await _db.BlogPosts
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && p.PostId != excludePostId && p.IsPublished)
            .OrderByDescending(p => p.PublishedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<List<BlogPost>> GetLatestAsync(int count, CancellationToken ct = default)
        => await _db.BlogPosts
            .Include(p => p.Category)
            .Include(p => p.Author)
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<List<BlogPost>> GetAllForAdminAsync(CancellationToken ct = default)
        => await _db.BlogPosts
            .Include(p => p.Category)
            .Include(p => p.Author)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);

    public async Task<List<BlogCategory>> GetCategoriesAsync(CancellationToken ct = default)
        => await _db.BlogCategories.OrderBy(c => c.SortOrder).ToListAsync(ct);

    public async Task<BlogPost> CreateAsync(BlogPost post, CancellationToken ct = default)
    {
        _db.BlogPosts.Add(post);
        await _db.SaveChangesAsync(ct);
        return post;
    }

    public async Task UpdateAsync(BlogPost post, CancellationToken ct = default)
    {
        post.UpdatedAt = DateTime.UtcNow;
        _db.BlogPosts.Update(post);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid postId, CancellationToken ct = default)
    {
        var post = await _db.BlogPosts.FindAsync([postId], ct);
        if (post is not null)
        {
            _db.BlogPosts.Remove(post);
            await _db.SaveChangesAsync(ct);
        }
    }
}
