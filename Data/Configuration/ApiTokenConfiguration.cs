using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable("ApiTokens");
        builder.HasKey(t => t.TokenId);

        builder.Property(t => t.TokenId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(t => t.Token).IsRequired().HasMaxLength(64);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100).HasDefaultValue("Add-in Token");
        builder.Property(t => t.IsActive).HasDefaultValue(true);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("UQ_ApiTokens_Token");

        builder.HasIndex(t => t.Token).HasDatabaseName("IX_ApiTokens_Token");

        builder.HasOne(t => t.User)
            .WithMany(u => u.ApiTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ApiTokens_Users");
    }
}
