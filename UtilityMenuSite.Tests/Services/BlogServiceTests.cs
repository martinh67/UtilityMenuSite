using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Services.Blog;

namespace UtilityMenuSite.Tests.Services;

/// <summary>
/// Unit tests for BlogService: post creation, publishing logic, updating, deletion, and pagination.
/// Key invariant tested: PublishedAt is set exactly when a post is first published, never overwritten.
/// </summary>
public class BlogServiceTests
{
    private readonly Mock<IBlogRepository>      _repoMock   = new();
    private readonly Mock<ILogger<BlogService>> _loggerMock = new();

    private BlogService CreateSut() => new(_repoMock.Object, _loggerMock.Object);

    private static CreateBlogPostDto BuildDto(bool isPublished = false, bool isFeatured = false) =>
        new()
        {
            Title          = "Test Post Title",
            Slug           = "test-post-title",
            CategoryId     = Guid.NewGuid(),
            Summary        = "A short test summary for this post.",
            Body           = "# Body\nSome markdown content here.",
            IsPublished    = isPublished,
            IsFeatured     = isFeatured,
            CoverImageUrl  = null,
            MetaTitle      = null,
            MetaDescription = null
        };

    // ── CreatePostAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePost_WhenPublished_SetsPublishedAtToNow()
    {
        BlogPost? saved = null;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()))
            .Callback<BlogPost, CancellationToken>((p, _) => saved = p)
            .ReturnsAsync((BlogPost p, CancellationToken _) => p);

        var before = DateTime.UtcNow;
        await CreateSut().CreatePostAsync(BuildDto(isPublished: true));
        var after = DateTime.UtcNow;

        saved!.IsPublished.Should().BeTrue();
        saved.PublishedAt.Should().NotBeNull();
        saved.PublishedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CreatePost_WhenDraft_LeavesPublishedAtNull()
    {
        BlogPost? saved = null;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()))
            .Callback<BlogPost, CancellationToken>((p, _) => saved = p)
            .ReturnsAsync((BlogPost p, CancellationToken _) => p);

        await CreateSut().CreatePostAsync(BuildDto(isPublished: false));

        saved!.IsPublished.Should().BeFalse();
        saved.PublishedAt.Should().BeNull("draft posts have no publish date");
    }

    [Fact]
    public async Task CreatePost_SetsCreatedAtAndUpdatedAt()
    {
        BlogPost? saved = null;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()))
            .Callback<BlogPost, CancellationToken>((p, _) => saved = p)
            .ReturnsAsync((BlogPost p, CancellationToken _) => p);

        var before = DateTime.UtcNow;
        await CreateSut().CreatePostAsync(BuildDto());
        var after = DateTime.UtcNow;

        saved!.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        saved.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CreatePost_MapsAllDtoFields()
    {
        BlogPost? saved = null;
        var categoryId = Guid.NewGuid();
        var authorId   = Guid.NewGuid();

        _repoMock.Setup(r => r.CreateAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()))
            .Callback<BlogPost, CancellationToken>((p, _) => saved = p)
            .ReturnsAsync((BlogPost p, CancellationToken _) => p);

        var dto = new CreateBlogPostDto
        {
            Title           = "My Post",
            Slug            = "my-post",
            CategoryId      = categoryId,
            AuthorId        = authorId,
            Summary         = "Summary here",
            Body            = "Body here",
            CoverImageUrl   = "https://example.com/img.jpg",
            IsPublished     = false,
            IsFeatured      = true,
            MetaTitle       = "Meta Title",
            MetaDescription = "Meta description"
        };

        await CreateSut().CreatePostAsync(dto);

        saved!.Title.Should().Be("My Post");
        saved.Slug.Should().Be("my-post");
        saved.CategoryId.Should().Be(categoryId);
        saved.AuthorId.Should().Be(authorId);
        saved.Summary.Should().Be("Summary here");
        saved.Body.Should().Be("Body here");
        saved.CoverImageUrl.Should().Be("https://example.com/img.jpg");
        saved.IsFeatured.Should().BeTrue();
        saved.MetaTitle.Should().Be("Meta Title");
        saved.MetaDescription.Should().Be("Meta description");
    }

    // ── UpdatePostAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePost_WhenPostNotFound_ThrowsInvalidOperationException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogPost?)null);

        await CreateSut()
            .Invoking(s => s.UpdatePostAsync(Guid.NewGuid(), BuildDto()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdatePost_WhenPublishingDraftPost_SetsPublishedAt()
    {
        var post = new BlogPost
        {
            PostId      = Guid.NewGuid(),
            IsPublished = false,
            PublishedAt = null
        };

        _repoMock.Setup(r => r.GetByIdAsync(post.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var before = DateTime.UtcNow;
        await CreateSut().UpdatePostAsync(post.PostId, BuildDto(isPublished: true));
        var after = DateTime.UtcNow;

        post.IsPublished.Should().BeTrue();
        post.PublishedAt.Should().NotBeNull();
        post.PublishedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task UpdatePost_WhenAlreadyPublished_DoesNotOverwritePublishedAt()
    {
        // Re-saving an already-published post must preserve the original publish date.
        var originalPublishedAt = DateTime.UtcNow.AddDays(-5);
        var post = new BlogPost
        {
            PostId      = Guid.NewGuid(),
            IsPublished = true,
            PublishedAt = originalPublishedAt
        };

        _repoMock.Setup(r => r.GetByIdAsync(post.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        await CreateSut().UpdatePostAsync(post.PostId, BuildDto(isPublished: true));

        post.PublishedAt.Should().Be(originalPublishedAt, "original publish date must not change");
    }

    [Fact]
    public async Task UpdatePost_WhenUnpublishing_DoesNotClearPublishedAt()
    {
        // The service does not clear PublishedAt when IsPublished is set back to false
        // (by design — PublishedAt records when it was first published).
        var originalPublishedAt = DateTime.UtcNow.AddDays(-3);
        var post = new BlogPost
        {
            PostId      = Guid.NewGuid(),
            IsPublished = true,
            PublishedAt = originalPublishedAt
        };

        _repoMock.Setup(r => r.GetByIdAsync(post.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        await CreateSut().UpdatePostAsync(post.PostId, BuildDto(isPublished: false));

        post.IsPublished.Should().BeFalse();
        post.PublishedAt.Should().Be(originalPublishedAt);
    }

    [Fact]
    public async Task UpdatePost_CallsRepositoryUpdate()
    {
        var post = new BlogPost { PostId = Guid.NewGuid() };
        _repoMock.Setup(r => r.GetByIdAsync(post.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        await CreateSut().UpdatePostAsync(post.PostId, BuildDto());

        _repoMock.Verify(r => r.UpdateAsync(post, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── PublishPostAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task PublishPost_WhenFound_SetsIsPublishedAndPublishedAt()
    {
        var post = new BlogPost { PostId = Guid.NewGuid(), IsPublished = false, PublishedAt = null };
        _repoMock.Setup(r => r.GetByIdAsync(post.PostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var before = DateTime.UtcNow;
        await CreateSut().PublishPostAsync(post.PostId);
        var after = DateTime.UtcNow;

        post.IsPublished.Should().BeTrue();
        post.PublishedAt.Should().NotBeNull();
        post.PublishedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        _repoMock.Verify(r => r.UpdateAsync(post, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishPost_WhenNotFound_IsNoOp()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogPost?)null);

        // Should complete without throwing and without calling Update
        await CreateSut().PublishPostAsync(Guid.NewGuid());

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<BlogPost>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── DeletePostAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePost_DelegatesToRepository()
    {
        var postId = Guid.NewGuid();

        await CreateSut().DeletePostAsync(postId);

        _repoMock.Verify(r => r.DeleteAsync(postId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetPublishedPostsAsync pagination ─────────────────────────────────────

    [Fact]
    public async Task GetPublishedPosts_CalculatesCorrectSkip()
    {
        _repoMock.Setup(r => r.GetPublishedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlogPost>());
        _repoMock.Setup(r => r.GetPublishedCountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        // Page 3, page size 12 → skip = (3-1) * 12 = 24
        await CreateSut().GetPublishedPostsAsync(categorySlug: null, page: 3, pageSize: 12);

        _repoMock.Verify(r => r.GetPublishedAsync(null, 24, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPublishedPosts_ReturnsCorrectPaginationMetadata()
    {
        var posts = new List<BlogPost> { new() { PostId = Guid.NewGuid() } };

        _repoMock.Setup(r => r.GetPublishedAsync(null, 0, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);
        _repoMock.Setup(r => r.GetPublishedCountAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(37);

        var result = await CreateSut().GetPublishedPostsAsync(categorySlug: null, page: 1, pageSize: 12);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(12);
        result.TotalCount.Should().Be(37);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPublishedPosts_WithCategoryFilter_PassesSlugToRepository()
    {
        _repoMock.Setup(r => r.GetPublishedAsync("tutorials", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlogPost>());
        _repoMock.Setup(r => r.GetPublishedCountAsync("tutorials", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await CreateSut().GetPublishedPostsAsync(categorySlug: "tutorials", page: 1, pageSize: 12);

        _repoMock.Verify(r => r.GetPublishedAsync("tutorials", 0, 12, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.GetPublishedCountAsync("tutorials", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetPostBySlugAsync / GetPostByIdAsync ─────────────────────────────────

    [Fact]
    public async Task GetPostBySlug_WhenFound_ReturnsPost()
    {
        var post = new BlogPost { PostId = Guid.NewGuid(), Slug = "my-slug" };
        _repoMock.Setup(r => r.GetBySlugAsync("my-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var result = await CreateSut().GetPostBySlugAsync("my-slug");

        result.Should().BeSameAs(post);
    }

    [Fact]
    public async Task GetPostBySlug_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogPost?)null);

        var result = await CreateSut().GetPostBySlugAsync("no-such-slug");

        result.Should().BeNull();
    }
}
