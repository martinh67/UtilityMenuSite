using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UtilityMenuSite.Data.Context;

namespace UtilityMenuSite.Tests.Infrastructure;

/// <summary>
/// Boots the full ASP.NET Core pipeline against an InMemory database.
/// Used by integration tests that need to exercise the real middleware stack
/// (antiforgery, forwarded headers, auth cookies, etc.).
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server with InMemory so tests don't need a real database.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseInMemoryDatabase("IntegrationTests_" + Guid.NewGuid()));
        });
    }
}
