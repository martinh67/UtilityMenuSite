using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Data.Repositories;
using UtilityMenuSite.Services.User;

namespace UtilityMenuSite.Tests.Integration;

/// <summary>
/// Integration tests for the full registration flow using a real in-memory database,
/// real UserManager, real UserRepository, and real UserService.
/// These tests mirror exactly what Register.razor HandleRegister() does.
/// </summary>
public class RegistrationFlowTests
{
    // Each test gets its own isolated in-memory database.
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
            {
                opts.Password.RequireDigit          = true;
                opts.Password.RequiredLength         = 8;
                opts.Password.RequireUppercase       = true;
                opts.Password.RequireNonAlphanumeric = false;
                opts.User.RequireUniqueEmail          = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserRepository,    UserRepository>();
        services.AddScoped<ILicenceRepository, LicenceRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IUserService,       UserService>();

        return services.BuildServiceProvider();
    }

    private static async Task SeedRolesAsync(IServiceScope scope)
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // ── Full registration chain ────────────────────────────────────────────────

    [Fact]
    public async Task RegisterUser_FullChain_CreatesIdentityUserAndAppUser()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const string email    = "test@example.com";
        const string password = "TestPass1";

        // Step 1 — create Identity user (UserManager.CreateAsync)
        var identityUser = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(identityUser, password);
        createResult.Succeeded.Should().BeTrue(because: string.Join(", ", createResult.Errors.Select(e => e.Description)));

        // Step 2 — assign role (UserManager.AddToRoleAsync)
        var roleResult = await userManager.AddToRoleAsync(identityUser, "User");
        roleResult.Succeeded.Should().BeTrue();

        // Step 3 — create Users record (UserService.RegisterFromIdentityAsync)
        var appUser = await userService.RegisterFromIdentityAsync(email, identityUser.Id);

        // Step 4 — verify Users record in DB
        var dbUser = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

        dbUser.Should().NotBeNull();
        dbUser!.IdentityId.Should().Be(identityUser.Id);
        dbUser.Email.Should().Be(email);
        dbUser.ApiToken.Should().NotBeNullOrWhiteSpace();
        dbUser.IsActive.Should().BeTrue();
        appUser.UserId.Should().Be(dbUser.UserId);
    }

    [Fact]
    public async Task RegisterUser_RoleAssigned_UserIsInUserRole()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var identityUser = new ApplicationUser { UserName = "role@example.com", Email = "role@example.com", EmailConfirmed = true };
        await userManager.CreateAsync(identityUser, "TestPass1");
        await userManager.AddToRoleAsync(identityUser, "User");

        var isInRole = await userManager.IsInRoleAsync(identityUser, "User");
        isInRole.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterUser_WhenRoleDoesNotExist_AddToRoleAsyncFails()
    {
        // Reproduces the production bug where SeedRolesAsync hadn't run.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        // Intentionally NOT seeding roles

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var identityUser = new ApplicationUser { UserName = "norole@example.com", Email = "norole@example.com", EmailConfirmed = true };
        await userManager.CreateAsync(identityUser, "TestPass1");

        // AddToRoleAsync throws InvalidOperationException (not a failed IdentityResult)
        // when the role doesn't exist — this is the actual failure mode that caused the production bug.
        await FluentActions.Invoking(() => userManager.AddToRoleAsync(identityUser, "User"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RegisterUser_DuplicateEmail_CreateAsyncFails()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var first = new ApplicationUser { UserName = "dup@example.com", Email = "dup@example.com", EmailConfirmed = true };
        await userManager.CreateAsync(first, "TestPass1");

        var second = new ApplicationUser { UserName = "dup@example.com", Email = "dup@example.com", EmailConfirmed = true };
        var result = await userManager.CreateAsync(second, "TestPass1");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "DuplicateEmail" || e.Code == "DuplicateUserName");
    }

    [Fact]
    public async Task RegisterUser_AppUserRecord_HasUniqueApiToken()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        var emails = new[] { "user1@example.com", "user2@example.com", "user3@example.com" };
        var tokens = new List<string>();

        foreach (var email in emails)
        {
            var identity = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(identity, "TestPass1");
            await userManager.AddToRoleAsync(identity, "User");
            var appUser = await userService.RegisterFromIdentityAsync(email, identity.Id);
            tokens.Add(appUser.ApiToken);
        }

        tokens.Should().OnlyHaveUniqueItems(because: "each user must get a unique API token");
    }

    [Fact]
    public async Task RegisterUser_SecondRegistrationWithSameEmail_LinksIdentityIdToExistingAppUser()
    {
        // Reproduces the Stripe-checkout-then-register flow:
        // app user was created by checkout (no IdentityId), then user registers on site.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const string email = "checkout@example.com";

        // Simulate checkout-created app user (no IdentityId)
        var checkoutUser = await userService.RegisterOrGetAsync(email);
        checkoutUser.IdentityId.Should().BeNull();

        // Now user registers on the site
        var identity = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(identity, "TestPass1");
        await userManager.AddToRoleAsync(identity, "User");
        await userService.RegisterFromIdentityAsync(email, identity.Id);

        // Verify the existing app user was updated, not duplicated
        var allUsers = await db.AppUsers.Where(u => u.Email == email).ToListAsync();
        allUsers.Should().HaveCount(1, because: "no duplicate Users record should be created");
        allUsers[0].IdentityId.Should().Be(identity.Id);
    }
}
