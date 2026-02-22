using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class BlogCategoryConfiguration : IEntityTypeConfiguration<BlogCategory>
{
    public void Configure(EntityTypeBuilder<BlogCategory> builder)
    {
        builder.ToTable("BlogCategories");
        builder.HasKey(c => c.CategoryId);

        builder.Property(c => c.CategoryId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(100);
        builder.Property(c => c.SortOrder).HasDefaultValue(0);

        builder.HasIndex(c => c.Slug)
            .IsUnique()
            .HasDatabaseName("UQ_BlogCategories_Slug");

        // Seed default categories
        builder.HasData(
            new BlogCategory { CategoryId = Guid.Parse("22222222-0000-0000-0000-000000000001"), Name = "News", Slug = "news", SortOrder = 1 },
            new BlogCategory { CategoryId = Guid.Parse("22222222-0000-0000-0000-000000000002"), Name = "Tutorials", Slug = "tutorials", SortOrder = 2 },
            new BlogCategory { CategoryId = Guid.Parse("22222222-0000-0000-0000-000000000003"), Name = "Updates", Slug = "updates", SortOrder = 3 }
        );
    }
}
