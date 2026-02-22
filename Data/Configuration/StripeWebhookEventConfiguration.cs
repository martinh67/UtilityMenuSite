using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class StripeWebhookEventConfiguration : IEntityTypeConfiguration<StripeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<StripeWebhookEvent> builder)
    {
        builder.ToTable("StripeWebhookEvents");
        builder.HasKey(e => e.WebhookEventId);

        builder.Property(e => e.WebhookEventId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.StripeEventId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.RawPayload).IsRequired();
        builder.Property(e => e.ReceivedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => e.StripeEventId)
            .IsUnique()
            .HasDatabaseName("UQ_StripeWebhookEvents_StripeEventId");
    }
}
