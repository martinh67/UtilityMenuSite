using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.UserId);

        builder.Property(u => u.UserId)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // IdentityId is nullable — users created via Stripe checkout have no Identity account
        // until they register on the site. The unique index is filtered to non-null values only.
        builder.Property(u => u.IdentityId)
            .HasMaxLength(450);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100);

        builder.Property(u => u.Organisation)
            .HasMaxLength(200);

        builder.Property(u => u.JobRole)
            .HasMaxLength(100);

        builder.Property(u => u.UsageInterests)
            .HasMaxLength(500);

        builder.Property(u => u.ExternalId)
            .HasMaxLength(200);

        builder.Property(u => u.ApiToken)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(u => u.UpdatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UQ_Users_Email");

        // Unique index on IdentityId filtered to non-null rows only, allowing multiple
        // checkout-created users with null IdentityId to coexist.
        builder.HasIndex(u => u.IdentityId)
            .IsUnique()
            .HasFilter("[IdentityId] IS NOT NULL")
            .HasDatabaseName("UQ_Users_IdentityId");

        builder.HasIndex(u => u.ApiToken)
            .IsUnique()
            .HasDatabaseName("UQ_Users_ApiToken");

        builder.HasIndex(u => u.Email)
            .HasDatabaseName("IX_Users_Email");

        builder.HasIndex(u => u.IdentityId)
            .HasDatabaseName("IX_Users_IdentityId");
    }
}
