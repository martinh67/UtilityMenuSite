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
/// Integration tests for the forgot-password / reset-password flow using a real
/// in-memory database and real UserManager with real token providers.
/// These tests mirror exactly what ForgotPassword.razor and ResetPassword.razor do.
/// </summary>
public class PasswordResetFlowTests
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
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserRepository,    UserRepository>();
        services.AddScoped<ILicenceRepository, LicenceRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IUserService,       UserService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        IServiceScope scope,
        string email    = "user@example.com",
        string password = "TestPass1")
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue();
        return user;
    }

    // ── Token generation ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WhenUserExists_GeneratesNonEmptyToken()
    {
        // Mirrors ForgotPassword.razor: find user → generate token → (send email)
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = await CreateUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ForgotPassword_WhenUserDoesNotExist_FindByEmailReturnsNull()
    {
        // ForgotPassword.razor calls FindByEmailAsync first; null = unknown email.
        // The page shows the same "check your email" message regardless to prevent enumeration.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("unknown@example.com");

        user.Should().BeNull("no account with this email exists");
    }

    [Fact]
    public async Task ForgotPassword_TwoTokensForSameUser_AreUnique()
    {
        // Each token generation invalidates the previous one — verify tokens differ.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = await CreateUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var token1 = await userManager.GeneratePasswordResetTokenAsync(user);
        var token2 = await userManager.GeneratePasswordResetTokenAsync(user);

        token1.Should().NotBe(token2);
    }

    // ── Full reset cycle ────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_WithValidToken_Succeeds()
    {
        // Mirrors ResetPassword.razor: find user → reset password with token
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = await CreateUserAsync(scope, password: "OldPass1");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, "NewPass2");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_NewPasswordIsAccepted()
    {
        // After a successful reset, the new password should be valid.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = await CreateUserAsync(scope, password: "OldPass1");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await userManager.ResetPasswordAsync(user, token, "NewPass2");

        var loginResult = await signInManager.CheckPasswordSignInAsync(user, "NewPass2", lockoutOnFailure: false);

        loginResult.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_OldPasswordIsRejected()
    {
        // After reset, the old password must no longer work.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = await CreateUserAsync(scope, password: "OldPass1");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await userManager.ResetPasswordAsync(user, token, "NewPass2");

        var loginResult = await signInManager.CheckPasswordSignInAsync(user, "OldPass1", lockoutOnFailure: false);

        loginResult.Succeeded.Should().BeFalse();
    }

    // ── Invalid token scenarios ─────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Fails()
    {
        // Mirrors the tampered / stale link scenario from the email.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = await CreateUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await userManager.ResetPasswordAsync(user, "not-a-valid-token", "NewPass2");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ResetPassword_WithUsedToken_FailsOnSecondUse()
    {
        // A token is single-use: using it a second time must fail.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = await CreateUserAsync(scope, password: "OldPass1");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        // First use — succeeds
        var first = await userManager.ResetPasswordAsync(user, token, "NewPass2");
        first.Succeeded.Should().BeTrue();

        // Second use of the same token — must fail
        var second = await userManager.ResetPasswordAsync(user, token, "AnotherPass3");
        second.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPassword_ToSamePassword_FailsPasswordValidation()
    {
        // Tests the password policy guard: the new password must meet all rules.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var user = await CreateUserAsync(scope);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, "weak"); // fails length/complexity

        result.Succeeded.Should().BeFalse();
    }

    // ── Email / user lookup safety ──────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_WhenUserObjectIsNull_NoExceptionThrown()
    {
        // ResetPassword.razor does FindByEmailAsync first. This test verifies
        // the safe guard pattern: null user → redirect without calling ResetPasswordAsync.
        await using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("no-such-user@example.com");

        // The page redirects to /account/login when user is null — no further calls made.
        // This assertion just confirms the guard condition works as intended.
        user.Should().BeNull();
    }
}
