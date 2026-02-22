using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Services.Blog;

public class BlogService : IBlogService
{
    private readonly IBlogRepository _blogRepo;
    private readonly ILogger<BlogService> _logger;

    public BlogService(IBlogRepository blogRepo, ILogger<BlogService> logger)
    {
        _blogRepo = blogRepo;
        _logger = logger;
    }

    public async Task<PagedResult<BlogPost>> GetPublishedPostsAsync(string? categorySlug, int page, int pageSize, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var items = await _blogRepo.GetPublishedAsync(categorySlug, skip, pageSize, ct);
        var total = await _blogRepo.GetPublishedCountAsync(categorySlug, ct);

        return new PagedResult<BlogPost>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<BlogPost?> GetPostBySlugAsync(string slug, CancellationToken ct = default)
        => await _blogRepo.GetBySlugAsync(slug, ct);

    public async Task<BlogPost?> GetPostByIdAsync(Guid postId, CancellationToken ct = default)
        => await _blogRepo.GetByIdAsync(postId, ct);

    public async Task<List<BlogPost>> GetLatestPostsAsync(int count, CancellationToken ct = default)
        => await _blogRepo.GetLatestAsync(count, ct);

    public async Task<List<BlogPost>> GetRelatedPostsAsync(Guid categoryId, Guid excludePostId, int count, CancellationToken ct = default)
        => await _blogRepo.GetRelatedAsync(categoryId, excludePostId, count, ct);

    public async Task<List<BlogCategory>> GetCategoriesAsync(CancellationToken ct = default)
        => await _blogRepo.GetCategoriesAsync(ct);

    public async Task<List<BlogPost>> GetAllForAdminAsync(CancellationToken ct = default)
        => await _blogRepo.GetAllForAdminAsync(ct);

    public async Task<BlogPost> CreatePostAsync(CreateBlogPostDto dto, CancellationToken ct = default)
    {
        var post = new BlogPost
        {
            CategoryId = dto.CategoryId,
            AuthorId = dto.AuthorId,
            Title = dto.Title,
            Slug = dto.Slug,
            Summary = dto.Summary,
            Body = dto.Body,
            CoverImageUrl = dto.CoverImageUrl,
            IsPublished = dto.IsPublished,
            IsFeatured = dto.IsFeatured,
            PublishedAt = dto.IsPublished ? DateTime.UtcNow : null,
            MetaTitle = dto.MetaTitle,
            MetaDescription = dto.MetaDescription,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _blogRepo.CreateAsync(post, ct);
    }

    public async Task<BlogPost> UpdatePostAsync(Guid postId, CreateBlogPostDto dto, CancellationToken ct = default)
    {
        var post = await _blogRepo.GetByIdAsync(postId, ct)
            ?? throw new InvalidOperationException("Post not found");

        post.CategoryId = dto.CategoryId;
        post.Title = dto.Title;
        post.Slug = dto.Slug;
        post.Summary = dto.Summary;
        post.Body = dto.Body;
        post.CoverImageUrl = dto.CoverImageUrl;
        post.MetaTitle = dto.MetaTitle;
        post.MetaDescription = dto.MetaDescription;
        post.IsFeatured = dto.IsFeatured;

        if (dto.IsPublished && !post.IsPublished)
            post.PublishedAt = DateTime.UtcNow;

        post.IsPublished = dto.IsPublished;

        await _blogRepo.UpdateAsync(post, ct);
        return post;
    }

    public async Task DeletePostAsync(Guid postId, CancellationToken ct = default)
        => await _blogRepo.DeleteAsync(postId, ct);

    public async Task PublishPostAsync(Guid postId, CancellationToken ct = default)
    {
        var post = await _blogRepo.GetByIdAsync(postId, ct);
        if (post is null) return;

        post.IsPublished = true;
        post.PublishedAt = DateTime.UtcNow;
        await _blogRepo.UpdateAsync(post, ct);
    }

    private static string GenerateSlug(string title)
        => title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("&", "and")
            .Replace(",", "")
            .Replace(".", "")
            .Trim('-');
}
