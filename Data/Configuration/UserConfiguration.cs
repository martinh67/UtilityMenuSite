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

        builder.Property(u => u.IdentityId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100);

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

        builder.HasIndex(u => u.IdentityId)
            .IsUnique()
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
