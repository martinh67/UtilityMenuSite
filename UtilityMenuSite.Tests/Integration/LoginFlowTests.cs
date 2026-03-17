using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Data.Repositories;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Services.User;

namespace UtilityMenuSite.Tests.Integration;

/// <summary>
/// Integration tests for the login flow using a real in-memory database,
/// real UserManager, and real SignInManager.
/// Tests credential validation — the same checks PasswordSignInAsync performs
/// before writing the auth cookie.
/// </summary>
public class LoginFlowTests
{
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
                opts.Lockout.MaxFailedAccessAttempts  = 3;
                opts.Lockout.DefaultLockoutTimeSpan   = TimeSpan.FromMinutes(5);
                opts.Lockout.AllowedForNewUsers        = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserRepository,    UserRepository>();
        services.AddScoped<ILicenceRepository, LicenceRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IUserService,       UserService>();

        return services.BuildServiceProvider();
    }

    private static async Task SeedRolesAndUserAsync(
        IServiceScope scope,
        string email    = "returning@example.com",
        string password = "TestPass1")
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "User" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, "User");
    }

    // ── Password validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithCorrectCredentials_PasswordCheckPasses()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAndUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("returning@example.com");
        user.Should().NotBeNull();

        var result = await signInManager.CheckPasswordSignInAsync(user!, "TestPass1", lockoutOnFailure: false);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WithWrongPassword_PasswordCheckFails()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAndUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("returning@example.com");
        user.Should().NotBeNull();

        var result = await signInManager.CheckPasswordSignInAsync(user!, "WrongPassword9", lockoutOnFailure: false);

        result.Succeeded.Should().BeFalse();
        result.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WhenUserDoesNotExist_FindByEmailReturnsNull()
    {
        // Register.razor / Login.razor both call FindByEmailAsync first.
        // If null, the flow returns SignInResult.Failed before any password check.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("ghost@example.com");

        user.Should().BeNull("no account exists for this email");
    }

    [Fact]
    public async Task Login_DirectPasswordCheck_WhenUserNotFound_ReturnsFalse()
    {
        // Simulate the guard in Login.razor: user is null → return false immediately.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("nobody@example.com");
        var passwordValid = user is not null && await userManager.CheckPasswordAsync(user, "TestPass1");

        passwordValid.Should().BeFalse();
    }

    // ── Lockout ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_AfterTooManyFailedAttempts_AccountIsLockedOut()
    {
        // MaxFailedAccessAttempts = 3 in BuildServiceProvider above.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAndUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("returning@example.com");
        user.Should().NotBeNull();

        // Exhaust allowed attempts
        for (var i = 0; i < 3; i++)
            await signInManager.CheckPasswordSignInAsync(user!, "WrongPassword9", lockoutOnFailure: true);

        var lockedOut = await userManager.IsLockedOutAsync(user!);
        lockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task Login_AfterLockout_CorrectPasswordStillFails()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAndUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("returning@example.com");

        // Lock the account explicitly (mirrors what happens after N failures in production)
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(5));

        var result = await signInManager.CheckPasswordSignInAsync(user!, "TestPass1", lockoutOnFailure: true);

        result.IsLockedOut.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Login_AfterSuccessfulLogin_FailedCountResets()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAndUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("returning@example.com");

        // One failed attempt
        await signInManager.CheckPasswordSignInAsync(user!, "WrongPassword9", lockoutOnFailure: true);
        var countBefore = await userManager.GetAccessFailedCountAsync(user!);
        countBefore.Should().Be(1);

        // Successful login resets the counter
        await signInManager.CheckPasswordSignInAsync(user!, "TestPass1", lockoutOnFailure: true);
        var countAfter = await userManager.GetAccessFailedCountAsync(user!);
        countAfter.Should().Be(0);
    }

    // ── Role verification ───────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ReturningUser_IsInUserRole()
    {
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        await SeedRolesAndUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("returning@example.com");
        var isInRole = await userManager.IsInRoleAsync(user!, "User");

        isInRole.Should().BeTrue();
    }

    [Fact]
    public async Task Login_ReturningUser_HasAnAppUserRecord()
    {
        // Verifies the full identity → app-user link is present for a returning user.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Full registration flow (same as Register.razor)
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roleManager.CreateAsync(new IdentityRole("User"));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        const string email = "fullreturn@example.com";
        var identityUser = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(identityUser, "TestPass1");
        await userManager.AddToRoleAsync(identityUser, "User");
        await userService.RegisterFromIdentityAsync(email, identityUser.Id);

        // Returning user: find identity + look up app user
        var foundIdentity = await userManager.FindByEmailAsync(email);
        var appUser = await userService.GetByIdentityIdAsync(foundIdentity!.Id);

        appUser.Should().NotBeNull();
        appUser!.Email.Should().Be(email);
        appUser.IdentityId.Should().Be(foundIdentity.Id);
    }
}
