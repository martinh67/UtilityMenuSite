using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class StripeCustomerConfiguration : IEntityTypeConfiguration<StripeCustomer>
{
    public void Configure(EntityTypeBuilder<StripeCustomer> builder)
    {
        builder.ToTable("StripeCustomers");
        builder.HasKey(sc => sc.StripeCustomerId);

        builder.Property(sc => sc.StripeCustomerId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sc => sc.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(sc => sc.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(sc => sc.UserId)
            .IsUnique()
            .HasDatabaseName("UQ_StripeCustomers_UserId");

        builder.HasIndex(sc => sc.UserId)
            .HasDatabaseName("IX_StripeCustomers_UserId");

        builder.HasOne(sc => sc.User)
            .WithOne(u => u.StripeCustomer)
            .HasForeignKey<StripeCustomer>(sc => sc.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StripeCustomers_Users");
    }
}
