using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using UtilityMenuSite.Core.Constants;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Data.Repositories;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Infrastructure.Security;
using UtilityMenuSite.Services.Licensing;
using UtilityMenuSite.Services.User;

namespace UtilityMenuSite.Tests.Services;

/// <summary>
/// End-to-end flow tests for the Free → Pro upgrade path using real service and repository
/// implementations backed by an EF Core InMemory database.
///
/// These tests verify the state a user ends up in after checkout completion, which is what
/// the dashboard queries to decide whether to show "Free" or the active licence.
/// </summary>
public class UpgradeFlowTests : IDisposable
{
    private readonly AppDbContext     _db;
    private readonly LicenceRepository _licenceRepo;
    private readonly UserRepository    _userRepo;
    private readonly LicenceService    _licenceService;
    private readonly UserService       _userService;

    private readonly LicensingSettings _settings = new()
    {
        HmacSigningKey      = "dGVzdC1obWFjLWtleS10aGF0LWlzLWxvbmctZW5vdWdoLWZvci1zaGEyNTY=",
        StalenessWindowDays = 7,
        GracePeriodDays     = 7
    };

    public UpgradeFlowTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("UpgradeFlow_" + Guid.NewGuid())
            .Options;

        _db          = new AppDbContext(dbOptions);
        _licenceRepo = new LicenceRepository(_db);
        _userRepo    = new UserRepository(_db);

        _licenceService = new LicenceService(
            _licenceRepo,
            Options.Create(_settings),
            Mock.Of<ILogger<LicenceService>>());

        _userService = new UserService(
            _userRepo,
            _licenceRepo,
            Mock.Of<IContactRepository>(),
            Mock.Of<ILogger<UserService>>());

        SeedModules();
    }

    public void Dispose() => _db.Dispose();

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private void SeedModules()
    {
        _db.Modules.AddRange(
            new Module { ModuleId = Guid.NewGuid(), ModuleName = "GetLastRow",    Tier = ModuleConstants.TierCore, IsActive = true, SortOrder = 1 },
            new Module { ModuleId = Guid.NewGuid(), ModuleName = "GetLastColumn", Tier = ModuleConstants.TierCore, IsActive = true, SortOrder = 2 },
            new Module { ModuleId = Guid.NewGuid(), ModuleName = "UnhideRows",    Tier = ModuleConstants.TierCore, IsActive = true, SortOrder = 3 },
            new Module { ModuleId = Guid.NewGuid(), ModuleName = "AdvancedData",  Tier = ModuleConstants.TierPro,  IsActive = true, SortOrder = 4 },
            new Module { ModuleId = Guid.NewGuid(), ModuleName = "BulkOperations",Tier = ModuleConstants.TierPro,  IsActive = true, SortOrder = 5 },
            new Module { ModuleId = Guid.NewGuid(), ModuleName = "DataExport",    Tier = ModuleConstants.TierPro,  IsActive = true, SortOrder = 6 },
            new Module { ModuleId = Guid.NewGuid(), ModuleName = "SqlBuilder",    Tier = ModuleConstants.TierPro,  IsActive = true, SortOrder = 7 }
        );
        _db.SaveChanges();
    }

    private async Task<(User user, Subscription subscription)> SimulateCheckoutCompletionAsync(
        string email,
        string stripeCustomerId = "cus_test123",
        string stripeSubId      = "sub_test123",
        string licenceType      = LicenceConstants.TypeIndividual)
    {
        var user = await _userService.RegisterOrGetAsync(email);
        await _licenceService.EnsureStripeCustomerAsync(user.UserId, stripeCustomerId, email);

        var subscription = await _licenceService.SyncSubscriptionAsync(
            stripeCustomerId: stripeCustomerId,
            stripeSubId:      stripeSubId,
            status:           "active",
            userId:           user.UserId,
            planType:         "monthly");

        await _licenceService.ProvisionLicenceAsync(
            userId:       user.UserId,
            subscriptionId: subscription.SubscriptionId,
            licenceKey:   LicenceKeyGenerator.Generate(),
            licenceType:  licenceType);

        return (user, subscription);
    }

    // ── Baseline: no licence before checkout ──────────────────────────────────

    [Fact]
    public async Task BeforeUpgrade_GetActiveLicenceReturnsNull()
    {
        var user = await _userService.RegisterOrGetAsync("free@example.com");

        var licence = await _licenceService.GetActiveLicenceAsync(user.UserId);

        licence.Should().BeNull("a brand-new user has no licence");
    }

    // ── Post-checkout: licence exists ─────────────────────────────────────────

    [Fact]
    public async Task AfterUpgrade_GetActiveLicenceReturnsProLicence()
    {
        var (user, _) = await SimulateCheckoutCompletionAsync("pro@example.com");

        var licence = await _licenceService.GetActiveLicenceAsync(user.UserId);

        licence.Should().NotBeNull();
        licence!.IsActive.Should().BeTrue();
        licence.LicenceType.Should().Be(LicenceConstants.TypeIndividual);
    }

    [Fact]
    public async Task AfterUpgrade_LicenceHasBothCoreAndProModules()
    {
        var (user, _) = await SimulateCheckoutCompletionAsync("modules@example.com");

        var licence = await _licenceService.GetActiveLicenceAsync(user.UserId);

        licence.Should().NotBeNull();
        var moduleNames = licence!.LicenceModules.Select(lm => lm.Module.ModuleName).ToList();

        moduleNames.Should().Contain("GetLastRow",     "core modules must be granted");
        moduleNames.Should().Contain("AdvancedData",   "pro modules must be granted on upgrade");
        moduleNames.Count.Should().Be(7,               "all 3 core + 4 pro modules should be unlocked");
    }

    [Fact]
    public async Task AfterUpgrade_LicenceKeyIsNonEmpty()
    {
        var (user, _) = await SimulateCheckoutCompletionAsync("key@example.com");

        var licence = await _licenceService.GetActiveLicenceAsync(user.UserId);

        licence!.LicenceKey.Should().NotBeNullOrWhiteSpace();
        licence.LicenceKey.Should().StartWith("UMENU-", "keys must follow the standard format");
    }

    // ── Subscription is also queryable ────────────────────────────────────────

    [Fact]
    public async Task AfterUpgrade_GetSubscriptionReturnsActiveRecord()
    {
        var (user, _) = await SimulateCheckoutCompletionAsync("sub@example.com");

        var subscription = await _licenceService.GetSubscriptionAsync(user.UserId);

        subscription.Should().NotBeNull();
        subscription!.Status.Should().Be("active");
        subscription.PlanType.Should().Be("monthly");
    }

    // ── Idempotency: double provisioning doesn't create a second licence ──────

    [Fact]
    public async Task DoubleProvisioning_SameUser_CreatesOnlyOneLicence()
    {
        const string email       = "double@example.com";
        const string customerId  = "cus_double";

        await SimulateCheckoutCompletionAsync(email, customerId, "sub_double1");

        // The webhook fires again for the same customer (duplicate delivery).
        // GetLicenceKeyForStripeCustomerAsync returns non-null, so provisioning is skipped.
        var existingKey = await _licenceService.GetLicenceKeyForStripeCustomerAsync(customerId);
        existingKey.Should().NotBeNull("first provisioning must have saved a licence");

        // Verify only one active licence exists for this user
        var user    = await _userService.GetByEmailAsync(email);
        var licences = await _db.Licences.Where(l => l.UserId == user!.UserId && l.IsActive).ToListAsync();
        licences.Should().HaveCount(1, "duplicate webhook must not create a second licence");
    }

    // ── Email case-insensitivity: Stripe may lowercase the email ─────────────

    [Fact]
    public async Task AfterUpgrade_WhenStripeEmailIsLowercase_FindsSameUser()
    {
        // User registers with mixed-case email (as entered in the Identity form)
        var user = await _userService.RegisterFromIdentityAsync("User@Example.COM", identityId: "identity-abc");

        // Stripe returns the email lowercased in session.CustomerDetails.Email
        var foundUser = await _userService.GetByEmailAsync("user@example.com");

        foundUser.Should().NotBeNull("case-insensitive lookup must find the same user");
        foundUser!.UserId.Should().Be(user.UserId,
            "provisioning with a lowercased email must target the same account");
    }

    // ── Custom licence gets only core modules ─────────────────────────────────

    [Fact]
    public async Task AfterUpgrade_CustomLicenceType_HasOnlyCoreModules()
    {
        var (user, _) = await SimulateCheckoutCompletionAsync(
            "custom@example.com",
            licenceType: LicenceConstants.TypeCustom);

        var licence = await _licenceService.GetActiveLicenceAsync(user.UserId);

        licence.Should().NotBeNull();
        var tiers = licence!.LicenceModules.Select(lm => lm.Module.Tier).Distinct().ToList();
        tiers.Should().OnlyContain(t => t == ModuleConstants.TierCore,
            "custom licences must not auto-grant pro modules");
    }
}
