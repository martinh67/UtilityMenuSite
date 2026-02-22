using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Configuration;

public class ContactSubmissionConfiguration : IEntityTypeConfiguration<ContactSubmission>
{
    public void Configure(EntityTypeBuilder<ContactSubmission> builder)
    {
        builder.ToTable("ContactSubmissions");
        builder.HasKey(c => c.SubmissionId);

        builder.Property(c => c.SubmissionId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.Subject).IsRequired().HasMaxLength(300);
        builder.Property(c => c.Message).IsRequired();
        builder.Property(c => c.IsResolved).HasDefaultValue(false);
        builder.Property(c => c.IpAddress).HasMaxLength(45);
        builder.Property(c => c.SubmittedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(c => new { c.IsResolved, c.SubmittedAt })
            .HasDatabaseName("IX_ContactSubmissions_IsResolved");
    }
}
