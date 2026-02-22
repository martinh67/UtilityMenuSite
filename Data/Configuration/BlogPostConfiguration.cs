using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("BlogPosts");
        builder.HasKey(p => p.PostId);

        builder.Property(p => p.PostId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(p => p.Title).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Summary).HasMaxLength(500);
        builder.Property(p => p.Body).IsRequired();
        builder.Property(p => p.CoverImageUrl).HasMaxLength(500);
        builder.Property(p => p.IsPublished).HasDefaultValue(false);
        builder.Property(p => p.IsFeatured).HasDefaultValue(false);
        builder.Property(p => p.MetaTitle).HasMaxLength(200);
        builder.Property(p => p.MetaDescription).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(p => p.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(p => p.Slug)
            .IsUnique()
            .HasDatabaseName("UQ_BlogPosts_Slug");

        builder.HasIndex(p => p.CategoryId).HasDatabaseName("IX_BlogPosts_CategoryId");

        builder.HasOne(p => p.Category)
            .WithMany(c => c.BlogPosts)
            .HasForeignKey(p => p.CategoryId)
            .HasConstraintName("FK_BlogPosts_Categories");

        builder.HasOne(p => p.Author)
            .WithMany(u => u.BlogPosts)
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_BlogPosts_Users");
    }
}
