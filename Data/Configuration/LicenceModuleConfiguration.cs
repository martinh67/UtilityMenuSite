using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class LicenceModuleConfiguration : IEntityTypeConfiguration<LicenceModule>
{
    public void Configure(EntityTypeBuilder<LicenceModule> builder)
    {
        builder.ToTable("LicenceModules");
        builder.HasKey(lm => lm.LicenceModuleId);

        builder.Property(lm => lm.LicenceModuleId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(lm => lm.GrantedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(lm => new { lm.LicenceId, lm.ModuleId })
            .IsUnique()
            .HasDatabaseName("UQ_LicenceModules");

        builder.HasIndex(lm => lm.LicenceId).HasDatabaseName("IX_LicenceModules_LicenceId");

        builder.HasOne(lm => lm.Licence)
            .WithMany(l => l.LicenceModules)
            .HasForeignKey(lm => lm.LicenceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_LicenceModules_Licences");

        builder.HasOne(lm => lm.Module)
            .WithMany(m => m.LicenceModules)
            .HasForeignKey(lm => lm.ModuleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LicenceModules_Modules");
    }
}
