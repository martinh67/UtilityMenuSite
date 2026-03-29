using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;
using Xunit;

namespace UtilityMenuSite.Tests.Infrastructure;

/// <summary>
/// Verifies that SeedData behaves correctly in development vs production:
/// - Development: creates a default admin user with hardcoded credentials
/// - Production: only seeds roles and promotes known admins; never creates
///   the default admin user (hardcoded passwords must not exist in production)
/// </summary>
public class SeedDataTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        var dbName = $"SeedDataTest_{Guid.NewGuid()}";
        services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));

        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Identity requires logging
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedAsync_WhenIsDevelopmentTrue_CreatesDefaultAdminUser()
    {
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedData.SeedAsync(scope.ServiceProvider, isDevelopment: true);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync("admin@utilitymenu.com");

        admin.Should().NotBeNull("default admin user should be created in development");
        (await userManager.IsInRoleAsync(admin!, "Admin")).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_WhenIsDevelopmentFalse_DoesNotCreateDefaultAdminUser()
    {
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedData.SeedAsync(scope.ServiceProvider, isDevelopment: false);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync("admin@utilitymenu.com");

        admin.Should().BeNull("default admin user must NOT be created in production");
    }

    [Fact]
    public async Task SeedAsync_WhenIsDevelopmentFalse_StillSeedsRoles()
    {
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedData.SeedAsync(scope.ServiceProvider, isDevelopment: false);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        (await roleManager.RoleExistsAsync("Admin")).Should().BeTrue("Admin role must always be seeded");
        (await roleManager.RoleExistsAsync("User")).Should().BeTrue("User role must always be seeded");
    }

    [Fact]
    public async Task SeedAsync_DefaultParameter_IsFalse()
    {
        // Calling SeedAsync without the isDevelopment parameter should default to
        // false (production-safe), so no admin user is created.
        using var sp = BuildServiceProvider();
        using var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedData.SeedAsync(scope.ServiceProvider);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync("admin@utilitymenu.com");

        admin.Should().BeNull("default parameter should be false (production-safe)");
    }
}
