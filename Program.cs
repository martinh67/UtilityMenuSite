using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UtilityMenuSite.Components;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Data.Repositories;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Services;
using UtilityMenuSite.Services.Blog;
using UtilityMenuSite.Services.Contact;
using UtilityMenuSite.Services.Licensing;
using UtilityMenuSite.Services.Payment;
using UtilityMenuSite.Services.User;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor ──────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// ── Data Protection ───────────────────────────────────────────────────────────
// Keys must survive app restarts (deployments restart the app, invalidating any
// in-memory keys and making antiforgery tokens generated before the restart
// unverifiable after it). Azure App Service exposes %HOME% which is backed by
// Azure Files and persists across restarts and deployments.
// SetApplicationName pins the key-purpose string so it doesn't change when the
// content root path changes between deployments.
var dpKeysPath = Path.Combine(
    Environment.GetEnvironmentVariable("HOME") ?? builder.Environment.ContentRootPath,
    "ASP.NET", "DataProtection-Keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new System.IO.DirectoryInfo(dpKeysPath))
    .SetApplicationName("UtilityMenuSite");

// ── Antiforgery ───────────────────────────────────────────────────────────────
// SecurePolicy = SameAsRequest: cookie gets Secure=true when the request scheme
// is https (which it is after UseForwardedHeaders runs). This ensures the cookie
// is issued correctly behind Azure's TLS-terminating proxy without being
// over-restricted to always-https (which would break local http development).
builder.Services.AddAntiforgery(opts =>
{
    opts.Cookie.Name = ".UtilityMenu.Antiforgery";
    opts.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    opts.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    opts.Cookie.HttpOnly = true;
});

// ── API Controllers ─────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── EF Core ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── ASP.NET Core Identity ────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
    {
        opts.Password.RequireDigit = true;
        opts.Password.RequiredLength = 8;
        opts.Password.RequireUppercase = true;
        opts.Password.RequireNonAlphanumeric = false;
        opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        opts.Lockout.MaxFailedAccessAttempts = 5;
        opts.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/account/login";
    opts.LogoutPath = "/account/logout";
    opts.AccessDeniedPath = "/account/access-denied";
    opts.ExpireTimeSpan = TimeSpan.FromDays(30);
    opts.SlidingExpiration = true;
    // Consistent with the antiforgery cookie — Secure only when the request is https,
    // which it will be in Azure (scheme set by UseForwardedHeaders).
    opts.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
});

// ── Authorization ────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("RequireAuthenticated", p => p.RequireAuthenticatedUser());
    opts.AddPolicy("RequireProLicence", p => p.RequireClaim("licence_type", "pro", "custom"));
    opts.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
});

// ── Strongly-typed settings ──────────────────────────────────────────────────
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<LicensingSettings>(builder.Configuration.GetSection("Licensing"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILicenceService, LicenceService>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<IStripeWebhookService, StripeWebhookService>();
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IVersionManifestService, VersionManifestService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILicenceRepository, LicenceRepository>();
builder.Services.AddScoped<IBlogRepository, BlogRepository>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();

// ── HTTP clients ─────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("sendgrid");

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(opts =>
{
    opts.AddPolicy<string>("licence-validate", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 60
            }));
    opts.AddPolicy<string>("contact-submit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromHours(1),
                PermitLimit = 3
            }));
});

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Database startup ──────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Relational providers (SQL Server) run migrations; InMemory (used by integration
    // tests via WebApplicationFactory) uses EnsureCreated instead.
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
    // Seed roles in all environments — roles must exist before any user can register.
    await SeedData.SeedRolesAsync(scope.ServiceProvider);
}

// ── Middleware pipeline ────────────────────────────────────────────────────────

// Azure App Service terminates SSL and forwards requests over HTTP internally.
// UseForwardedHeaders must come first so that subsequent middleware (antiforgery,
// HTTPS redirect, auth cookies) all see the correct scheme and remote IP.
// KnownNetworks/KnownProxies are cleared because Azure's internal load balancer
// IP is not in the default loopback-only list — without this the middleware
// silently ignores X-Forwarded-Proto and the scheme stays "http", breaking
// antiforgery. Azure's infrastructure controls these headers, so trusting all
// sources is safe inside an App Service.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Health probe — used by Azure App Service / load balancer health checks.
app.MapHealthChecks("/health");

app.MapControllers();
app.MapRazorComponents<UtilityMenuSite.App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
