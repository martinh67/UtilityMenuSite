using System;
using System.Collections.Generic;

namespace UtilityMenuSite.Core.Models.Api;

public record BlogCategoryDto(Guid CategoryId, string Name, string Slug, int SortOrder);

public record BlogPostDto(
    Guid PostId,
    string Title,
    string Slug,
    string? Summary,
    string? Body,
    string? CoverImageUrl,
    string? MetaTitle,
    string? MetaDescription,
    bool IsPublished,
    bool IsFeatured,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? CategoryId,
    string? CategoryName);

public record BlogPostListResponse(IReadOnlyList<BlogPostDto> Posts, int Total);

public record CreateOrUpdateBlogPostRequest(
    string Title,
    string Slug,
    string? Summary,
    string Body,
    string? CoverImageUrl,
    string? MetaTitle,
    string? MetaDescription,
    bool IsPublished,
    bool IsFeatured,
    Guid? CategoryId);
