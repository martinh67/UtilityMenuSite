using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");
        builder.HasKey(s => s.SubscriptionId);

        builder.Property(s => s.SubscriptionId)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.StripeCustomerId).IsRequired().HasMaxLength(100);
        builder.Property(s => s.StripeSubscriptionId).IsRequired().HasMaxLength(100);
        builder.Property(s => s.StripePriceId).HasMaxLength(100);
        builder.Property(s => s.Status).IsRequired().HasMaxLength(50);
        builder.Property(s => s.PlanType).IsRequired().HasMaxLength(50);
        builder.Property(s => s.CancelAtPeriodEnd).HasDefaultValue(false);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(s => s.StripeSubscriptionId)
            .IsUnique()
            .HasDatabaseName("UQ_Subscriptions_StripeId");

        builder.HasIndex(s => s.UserId).HasDatabaseName("IX_Subscriptions_UserId");
        builder.HasIndex(s => s.StripeCustomerId).HasDatabaseName("IX_Subscriptions_StripeCustomerId");
        builder.HasIndex(s => s.StripeSubscriptionId).HasDatabaseName("IX_Subscriptions_StripeSubscriptionId");

        builder.HasOne(s => s.User)
            .WithMany(u => u.Subscriptions)
            .HasForeignKey(s => s.UserId)
            .HasConstraintName("FK_Subscriptions_Users");
    }
}
