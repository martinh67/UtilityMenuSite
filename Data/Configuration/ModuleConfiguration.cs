using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToTable("Modules");
        builder.HasKey(m => m.ModuleId);

        builder.Property(m => m.ModuleId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(m => m.ModuleName).IsRequired().HasMaxLength(100);
        builder.Property(m => m.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Description).HasMaxLength(500);
        builder.Property(m => m.Tier).IsRequired().HasMaxLength(30);
        builder.Property(m => m.IsActive).HasDefaultValue(true);
        builder.Property(m => m.SortOrder).HasDefaultValue(0);
        builder.Property(m => m.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(m => m.ModuleName)
            .IsUnique()
            .HasDatabaseName("UQ_Modules_ModuleName");

        // Seed data matching ribbon XML control.Tag values
        builder.HasData(
            new Module { ModuleId = Guid.Parse("11111111-0000-0000-0000-000000000001"), ModuleName = "GetLastRow", DisplayName = "Get Last Row", Tier = "core", IsActive = true, SortOrder = 1, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Module { ModuleId = Guid.Parse("11111111-0000-0000-0000-000000000002"), ModuleName = "GetLastColumn", DisplayName = "Get Last Column", Tier = "core", IsActive = true, SortOrder = 2, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Module { ModuleId = Guid.Parse("11111111-0000-0000-0000-000000000003"), ModuleName = "UnhideRows", DisplayName = "Unhide Rows", Tier = "core", IsActive = true, SortOrder = 3, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Module { ModuleId = Guid.Parse("11111111-0000-0000-0000-000000000004"), ModuleName = "AdvancedData", DisplayName = "Advanced Data Tools", Tier = "pro", IsActive = true, SortOrder = 4, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Module { ModuleId = Guid.Parse("11111111-0000-0000-0000-000000000005"), ModuleName = "BulkOperations", DisplayName = "Bulk Operations", Tier = "pro", IsActive = true, SortOrder = 5, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Module { ModuleId = Guid.Parse("11111111-0000-0000-0000-000000000006"), ModuleName = "DataExport", DisplayName = "Data Export", Tier = "pro", IsActive = true, SortOrder = 6, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Module { ModuleId = Guid.Parse("11111111-0000-0000-0000-000000000007"), ModuleName = "SqlBuilder", DisplayName = "SQL Builder", Tier = "pro", IsActive = true, SortOrder = 7, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
