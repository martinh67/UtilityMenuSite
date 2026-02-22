using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> builder)
    {
        builder.ToTable("Machines");
        builder.HasKey(m => m.MachineId);

        builder.Property(m => m.MachineId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(m => m.MachineFingerprint).IsRequired().HasMaxLength(200);
        builder.Property(m => m.MachineName).HasMaxLength(200);
        builder.Property(m => m.IsActive).HasDefaultValue(true);
        builder.Property(m => m.FirstSeenAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(m => m.LastSeenAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(m => m.LicenceId).HasDatabaseName("IX_Machines_LicenceId");
        builder.HasIndex(m => new { m.LicenceId, m.MachineFingerprint })
            .HasDatabaseName("IX_Machines_LicenceId_Fingerprint");

        builder.HasOne(m => m.Licence)
            .WithMany(l => l.Machines)
            .HasForeignKey(m => m.LicenceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Machines_Licences");
    }
}
