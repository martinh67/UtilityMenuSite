using System.Threading.RateLimiting;
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

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Supplies Task<AuthenticationState> as a cascading value to all Blazor components
builder.Services.AddCascadingAuthenticationState();

// API Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// EF Core — SQLite for local dev (macOS/Linux), SQL Server in staging/production
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// ASP.NET Core Identity
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
});

// Authorization Policies
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("RequireAuthenticated", p => p.RequireAuthenticatedUser());
    opts.AddPolicy("RequireProLicence", p => p.RequireClaim("licence_type", "pro", "custom"));
    opts.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
});

// Configuration binding
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<LicensingSettings>(builder.Configuration.GetSection("Licensing"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Application Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILicenceService, LicenceService>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<IStripeWebhookService, StripeWebhookService>();
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IVersionManifestService, VersionManifestService>();

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILicenceRepository, LicenceRepository>();
builder.Services.AddScoped<IBlogRepository, BlogRepository>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();

// Rate limiting (using AddPolicy so no extension-method namespace issues)
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

// HttpContext accessor for controllers
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

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

app.MapControllers();
app.MapRazorComponents<UtilityMenuSite.App>()
    .AddInteractiveServerRenderMode();

// Ensure database is created and seeded in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await SeedData.SeedAsync(scope.ServiceProvider);
}

app.Run();
