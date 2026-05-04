using Azure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using UtilityMenuSite.Components;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Services.Api;
using UtilityMenuSite.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// ── Application Insights ─────────────────────────────────────────────────────
var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(aiConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(opts =>
        opts.ConnectionString = aiConnectionString);
}

// ── Blazor ──────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MVC controllers — currently used only by DownloadController to proxy the
// authenticated installer stream from the API. Razor Components handles the
// rest of the Site.
builder.Services.AddControllers();

builder.Services.AddCascadingAuthenticationState();

// ── Data Protection ──────────────────────────────────────────────────────────
// Keys must survive app restarts so the auth cookie (which carries the JWT
// access + refresh tokens) remains decryptable after a redeploy. Two-tier
// strategy mirroring UtilityMenuAPI:
//  - UAT/Prod: persist to Azure Blob (DataProtection:BlobUri from KV via the
//    Terraform-managed shared-data-protection-blob-uri secret). Without
//    persistent keys, every redeploy invalidates auth cookies and silently
//    logs out every user. Fail fast in non-Dev if the URI is missing — this
//    is a deploy-blocking config error, not a soft warning.
//  - Development: persist to %HOME%/ASP.NET/DataProtection-Keys filesystem.
{
    var dpBuilder = builder.Services.AddDataProtection().SetApplicationName("UtilityMenuSite");
    var blobUri = builder.Configuration["DataProtection:BlobUri"];
    if (!builder.Environment.IsDevelopment())
    {
        if (string.IsNullOrWhiteSpace(blobUri))
            throw new InvalidOperationException(
                "DataProtection:BlobUri is required outside of Development. " +
                "Set via DataProtection__BlobUri (Terraform-managed) — without it, redeploys silently sign out every user.");
        dpBuilder.PersistKeysToAzureBlobStorage(new Uri(blobUri), new DefaultAzureCredential());
    }
    else
    {
        var fallback = Path.Combine(
            Environment.GetEnvironmentVariable("HOME") ?? builder.Environment.ContentRootPath,
            "ASP.NET", "DataProtection-Keys");
        dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(fallback));
    }
}

// ── Cookie auth (replaces Identity-coupled cookie scheme) ────────────────────
// The Site is now a pure BFF: the auth cookie holds the user's claims AND the
// JWT access + refresh tokens (as claims) for the API. There is no local user
// store — all credentials are validated server-side via /api/v1/account/login.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opts =>
    {
        opts.LoginPath = "/account/login";
        opts.LogoutPath = "/account/logout";
        opts.AccessDeniedPath = "/account/access-denied";
        opts.ExpireTimeSpan = TimeSpan.FromDays(7); // matches API Jwt:RefreshChainMaxDays
        opts.SlidingExpiration = true;
        opts.Cookie.Name = "UtilityMenu.Auth";
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SameSite = SameSiteMode.Lax;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("RequireAuthenticated", p => p.RequireAuthenticatedUser());
    opts.AddPolicy("RequireProLicence", p => p.RequireClaim("licence_type", "individual", "team", "lifetime", "custom"));
    opts.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
});

// ── UtilityMenuAPI client ────────────────────────────────────────────────────
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("Api"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IJwtTokenStorage, HttpContextJwtTokenStorage>();
builder.Services.AddScoped<IAuthCookieIssuer, AuthCookieIssuer>();
builder.Services.AddTransient<ApiAuthHandler>();
builder.Services.AddHttpClient<IUtilityMenuApiClient, UtilityMenuApiClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiSettings>>().Value;
    var env = sp.GetRequiredService<IHostEnvironment>();
    if (string.IsNullOrWhiteSpace(settings.BaseUrl))
    {
        if (!env.IsDevelopment())
            throw new InvalidOperationException(
                "Api:BaseUrl is not configured. In UAT/Prod this must be set via the " +
                "Api__BaseUrl env var (Terraform-managed) or appsettings.{Environment}.json.");
        // Dev fallback — local API on the standard ASP.NET Core HTTPS port.
        client.BaseAddress = new Uri("https://localhost:7001");
    }
    else
    {
        client.BaseAddress = new Uri(settings.BaseUrl);
    }
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<ApiAuthHandler>();

// ── Health checks ─────────────────────────────────────────────────────────────
// No DbContextCheck — the API owns the database. A simple /health endpoint
// returns 200 if the Site process is up; deeper checks can probe the API.
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Startup diagnostics ───────────────────────────────────────────────────────
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("UtilityMenuSite starting — Environment: {Environment}", app.Environment.EnvironmentName);

if (string.IsNullOrWhiteSpace(builder.Configuration["Api:BaseUrl"]))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Api:BaseUrl is required outside of Development. " +
            "Set via Api__BaseUrl env var (Terraform-managed) or appsettings.{Environment}.json.");
    startupLogger.LogWarning("Api:BaseUrl is not configured — API calls will hit https://localhost:7001 by default");
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
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
    app.UseStatusCodePagesWithReExecute("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapRazorComponents<UtilityMenuSite.App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
