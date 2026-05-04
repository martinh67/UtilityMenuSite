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
// access + refresh tokens) remains decryptable after a redeploy. UAT/Prod
// persist to Azure Files via the App Service %HOME% path; Dev falls back to
// the content-root-relative path. Once Phase 2's blob storage backing is in
// place, this can switch to PersistKeysToAzureBlobStorage.
var dpKeysPath = Path.Combine(
    Environment.GetEnvironmentVariable("HOME") ?? builder.Environment.ContentRootPath,
    "ASP.NET", "DataProtection-Keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new System.IO.DirectoryInfo(dpKeysPath))
    .SetApplicationName("UtilityMenuSite");

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
    var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://localhost:7001" : settings.BaseUrl;
    client.BaseAddress = new Uri(baseUrl);
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
    startupLogger.LogWarning("Api:BaseUrl is not configured — API calls will hit https://localhost:7001 by default");

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
