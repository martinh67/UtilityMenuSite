using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Context;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<User> AppUsers { get; set; } = null!;
    public DbSet<StripeCustomer> StripeCustomers { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<Licence> Licences { get; set; } = null!;
    public DbSet<Module> Modules { get; set; } = null!;
    public DbSet<LicenceModule> LicenceModules { get; set; } = null!;
    public DbSet<Machine> Machines { get; set; } = null!;
    public DbSet<StripeWebhookEvent> StripeWebhookEvents { get; set; } = null!;
    public DbSet<ApiToken> ApiTokens { get; set; } = null!;
    public DbSet<BlogCategory> BlogCategories { get; set; } = null!;
    public DbSet<BlogPost> BlogPosts { get; set; } = null!;
    public DbSet<ContactSubmission> ContactSubmissions { get; set; } = null!;
    public DbSet<UsageEvent> UsageEvents { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
