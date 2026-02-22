using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class LicenceConfiguration : IEntityTypeConfiguration<Licence>
{
    public void Configure(EntityTypeBuilder<Licence> builder)
    {
        builder.ToTable("Licences");
        builder.HasKey(l => l.LicenceId);

        builder.Property(l => l.LicenceId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(l => l.LicenceKey).IsRequired().HasMaxLength(30);
        builder.Property(l => l.LicenceType).IsRequired().HasMaxLength(30);
        builder.Property(l => l.MaxActivations).HasDefaultValue(2);
        builder.Property(l => l.IsActive).HasDefaultValue(true);
        builder.Property(l => l.Signature).HasMaxLength(512);
        builder.Property(l => l.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(l => l.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(l => l.LicenceKey)
            .IsUnique()
            .HasDatabaseName("UQ_Licences_LicenceKey");

        builder.HasIndex(l => l.UserId).HasDatabaseName("IX_Licences_UserId");
        builder.HasIndex(l => l.SubscriptionId).HasDatabaseName("IX_Licences_SubscriptionId");
        builder.HasIndex(l => l.LicenceKey).HasDatabaseName("IX_Licences_LicenceKey");

        builder.HasOne(l => l.User)
            .WithMany(u => u.Licences)
            .HasForeignKey(l => l.UserId)
            .HasConstraintName("FK_Licences_Users");

        builder.HasOne(l => l.Subscription)
            .WithMany(s => s.Licences)
            .HasForeignKey(l => l.SubscriptionId)
            .HasConstraintName("FK_Licences_Subscriptions");
    }
}
