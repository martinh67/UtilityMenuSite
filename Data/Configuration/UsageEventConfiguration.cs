using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class UsageEventConfiguration : IEntityTypeConfiguration<UsageEvent>
{
    public void Configure(EntityTypeBuilder<UsageEvent> builder)
    {
        builder.ToTable("UsageEvents");
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.OccurredAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => new { e.UserId, e.OccurredAt }).HasDatabaseName("IX_UsageEvents_UserId");
        builder.HasIndex(e => new { e.EventType, e.OccurredAt }).HasDatabaseName("IX_UsageEvents_EventType");

        builder.HasOne(e => e.User)
            .WithMany(u => u.UsageEvents)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_UsageEvents_Users");

        builder.HasOne(e => e.Licence)
            .WithMany(l => l.UsageEvents)
            .HasForeignKey(e => e.LicenceId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_UsageEvents_Licences");

        builder.HasOne(e => e.Machine)
            .WithMany(m => m.UsageEvents)
            .HasForeignKey(e => e.MachineId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_UsageEvents_Machines");

        builder.HasOne(e => e.Module)
            .WithMany(m => m.UsageEvents)
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_UsageEvents_Modules");
    }
}
