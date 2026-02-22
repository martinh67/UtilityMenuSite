using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.AuditLogId);

        builder.Property(a => a.AuditLogId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityName).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.OccurredAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(a => new { a.UserId, a.OccurredAt }).HasDatabaseName("IX_AuditLogs_UserId");
        builder.HasIndex(a => new { a.Action, a.OccurredAt }).HasDatabaseName("IX_AuditLogs_Action");

        builder.HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_AuditLogs_Users");
    }
}
