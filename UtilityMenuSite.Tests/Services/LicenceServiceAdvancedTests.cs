using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using UtilityMenuSite.Core.Constants;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Services.Licensing;

namespace UtilityMenuSite.Tests.Services;

/// <summary>
/// Additional unit tests for LicenceService covering scenarios not in LicenceServiceTests:
/// DeactivateMachine, DeactivateMachineByFingerprint, ProvisionLicence (pro vs custom),
/// SyncSubscription, EnsureStripeCustomer, and perpetual-licence validation.
/// </summary>
public class LicenceServiceAdvancedTests
{
    private readonly Mock<ILicenceRepository>      _repoMock   = new();
    private readonly Mock<ILogger<LicenceService>> _loggerMock = new();
    private readonly LicensingSettings _settings = new()
    {
        HmacSigningKey    = "dGVzdC1obWFjLWtleS10aGF0LWlzLWxvbmctZW5vdWdoLWZvci1zaGEyNTY=",
        StalenessWindowDays = 7,
        GracePeriodDays   = 7
    };

    private LicenceService CreateSut() =>
        new(_repoMock.Object, Options.Create(_settings), _loggerMock.Object);

    private static Licence BuildLicence(
        bool isActive = true,
        DateTime? expiresAt = null,
        int maxActivations = 2,
        string licenceType = LicenceConstants.TypeIndividual) => new()
    {
        LicenceId      = Guid.NewGuid(),
        LicenceKey     = "UMENU-TEST-TEST-TEST",
        LicenceType    = licenceType,
        IsActive       = isActive,
        ExpiresAt      = expiresAt ?? DateTime.UtcNow.AddYears(1),
        MaxActivations = maxActivations,
        UserId         = Guid.NewGuid(),
        LicenceModules = [],
        Machines       = []
    };

    // ── ValidateLicence — edge cases ──────────────────────────────────────────

    [Fact]
    public async Task ValidateLicence_WithNoExpiryDate_IsValid()
    {
        // Lifetime / perpetual licences have no expiry date — must be treated as valid forever.
        var licence = new Licence
        {
            LicenceId      = Guid.NewGuid(),
            LicenceKey     = "UMENU-LIFE-LIFE-LIFE",
            LicenceType    = LicenceConstants.TypeLifetime,
            IsActive       = true,
            ExpiresAt      = null,  // perpetual
            MaxActivations = 2,
            LicenceModules = [],
            Machines       = []
        };
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);

        var result = await CreateSut().ValidateLicenceAsync(licence.LicenceKey);

        result.IsValid.Should().BeTrue();
        result.ExpiresAt.Should().BeNull();
    }

    // ── DeactivateMachineAsync ────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateMachine_WhenMachineExists_ReturnsTrueAndRecordsUsageEvent()
    {
        var machineId = Guid.NewGuid();
        var licenceId = Guid.NewGuid();
        var machine = new Machine { MachineId = machineId, LicenceId = licenceId };

        _repoMock.Setup(r => r.DeactivateMachineByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.GetMachineByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);

        var result = await CreateSut().DeactivateMachineAsync(machineId);

        result.Should().BeTrue();
        _repoMock.Verify(r => r.RecordUsageEventAsync(
            It.Is<UsageEvent>(e => e.MachineId == machineId && e.EventType == EventTypeConstants.MachineDeactivation),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateMachine_WhenMachineNotFound_ReturnsFalse()
    {
        _repoMock.Setup(r => r.DeactivateMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateSut().DeactivateMachineAsync(Guid.NewGuid());

        result.Should().BeFalse();
        _repoMock.Verify(r => r.RecordUsageEventAsync(It.IsAny<UsageEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── DeactivateMachineByFingerprintAsync ───────────────────────────────────

    [Fact]
    public async Task DeactivateByFingerprint_WhenLicenceNotFound_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Licence?)null);

        var result = await CreateSut().DeactivateMachineByFingerprintAsync("UMENU-FAKE-FAKE-FAKE", "fp-12345678");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateByFingerprint_WhenMachineNotFound_ReturnsFalse()
    {
        var licence = BuildLicence();
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);
        _repoMock.Setup(r => r.GetMachineAsync(licence.LicenceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        var result = await CreateSut().DeactivateMachineByFingerprintAsync(licence.LicenceKey, "fp-12345678");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateByFingerprint_WhenMachineInactive_ReturnsFalse()
    {
        var licence = BuildLicence();
        var machine = new Machine { MachineId = Guid.NewGuid(), IsActive = false };

        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);
        _repoMock.Setup(r => r.GetMachineAsync(licence.LicenceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);

        var result = await CreateSut().DeactivateMachineByFingerprintAsync(licence.LicenceKey, "fp-12345678");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateByFingerprint_WhenMachineActive_DeactivatesAndReturnsTrue()
    {
        var licence   = BuildLicence();
        var machineId = Guid.NewGuid();
        var machine   = new Machine { MachineId = machineId, LicenceId = licence.LicenceId, IsActive = true };

        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);
        _repoMock.Setup(r => r.GetMachineAsync(licence.LicenceId, "fp-abc12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);
        _repoMock.Setup(r => r.DeactivateMachineByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.GetMachineByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);

        var result = await CreateSut().DeactivateMachineByFingerprintAsync(licence.LicenceKey, "fp-abc12345");

        result.Should().BeTrue();
        _repoMock.Verify(r => r.DeactivateMachineByIdAsync(machineId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ProvisionLicenceAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ProvisionLicence_WhenProType_GrantsCoreAndProModules()
    {
        var userId         = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var coreModule     = new Module { ModuleId = Guid.NewGuid(), ModuleName = "GetLastRow",     Tier = ModuleConstants.TierCore };
        var proModule      = new Module { ModuleId = Guid.NewGuid(), ModuleName = "AdvancedData",   Tier = ModuleConstants.TierPro };

        _repoMock.Setup(r => r.CreateAsync(It.IsAny<Licence>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Licence l, CancellationToken _) => l);

        _repoMock.Setup(r => r.GetModulesByTiersAsync(
                It.Is<IEnumerable<string>>(t => t.Contains(ModuleConstants.TierCore) && t.Contains(ModuleConstants.TierPro)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Module> { coreModule, proModule });

        var result = await CreateSut().ProvisionLicenceAsync(
            userId, subscriptionId, "UMENU-PRO1-PRO1-PRO1", LicenceConstants.TypeIndividual);

        result.LicenceType.Should().Be(LicenceConstants.TypeIndividual);
        result.IsActive.Should().BeTrue();

        _repoMock.Verify(r => r.GetModulesByTiersAsync(
            It.Is<IEnumerable<string>>(t => t.Contains(ModuleConstants.TierCore) && t.Contains(ModuleConstants.TierPro)),
            It.IsAny<CancellationToken>()), Times.Once);

        _repoMock.Verify(r => r.AddLicenceModulesAsync(
            It.Is<IEnumerable<LicenceModule>>(lm => lm.Count() == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProvisionLicence_WhenCustomType_GrantsCoreModulesOnly()
    {
        // Custom licences start with only core modules — pro/custom granted separately via admin.
        var userId         = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var coreModule     = new Module { ModuleId = Guid.NewGuid(), ModuleName = "GetLastRow", Tier = ModuleConstants.TierCore };

        _repoMock.Setup(r => r.CreateAsync(It.IsAny<Licence>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Licence l, CancellationToken _) => l);

        _repoMock.Setup(r => r.GetModulesByTiersAsync(
                It.Is<IEnumerable<string>>(t => t.Contains(ModuleConstants.TierCore) && !t.Contains(ModuleConstants.TierPro)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Module> { coreModule });

        await CreateSut().ProvisionLicenceAsync(
            userId, subscriptionId, "UMENU-CUST-CUST-CUST", LicenceConstants.TypeCustom);

        _repoMock.Verify(r => r.GetModulesByTiersAsync(
            It.Is<IEnumerable<string>>(t => !t.Contains(ModuleConstants.TierPro)),
            It.IsAny<CancellationToken>()), Times.Once);

        _repoMock.Verify(r => r.AddLicenceModulesAsync(
            It.Is<IEnumerable<LicenceModule>>(lm => lm.Count() == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProvisionLicence_SetsDefaultMaxActivations()
    {
        Licence? created = null;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<Licence>(), It.IsAny<CancellationToken>()))
            .Callback<Licence, CancellationToken>((l, _) => created = l)
            .ReturnsAsync((Licence l, CancellationToken _) => l);
        _repoMock.Setup(r => r.GetModulesByTiersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Module>());

        await CreateSut().ProvisionLicenceAsync(Guid.NewGuid(), Guid.NewGuid(), "UMENU-TEST-TEST-TEST", LicenceConstants.TypeIndividual);

        created!.MaxActivations.Should().Be(LicenceConstants.DefaultMaxActivations);
    }

    // ── SyncSubscriptionAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SyncSubscription_WhenNew_CreatesSubscription()
    {
        _repoMock.Setup(r => r.GetSubscriptionByStripeIdAsync("sub_new", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);
        _repoMock.Setup(r => r.CreateSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        var userId = Guid.NewGuid();
        var result = await CreateSut().SyncSubscriptionAsync("cus_123", "sub_new", "active", userId, "monthly");

        result.Status.Should().Be("active");
        result.PlanType.Should().Be("monthly");
        result.UserId.Should().Be(userId);

        _repoMock.Verify(r => r.CreateSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncSubscription_WhenExisting_UpdatesStatusOnly()
    {
        var existing = new Subscription
        {
            SubscriptionId       = Guid.NewGuid(),
            StripeSubscriptionId = "sub_existing",
            Status               = "active",
            PlanType             = "monthly",
            CreatedAt            = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt            = DateTime.UtcNow.AddDays(-1)
        };

        _repoMock.Setup(r => r.GetSubscriptionByStripeIdAsync("sub_existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateSut().SyncSubscriptionAsync("cus_123", "sub_existing", "past_due", Guid.NewGuid(), "monthly");

        result.Status.Should().Be("past_due");
        result.Should().BeSameAs(existing);

        _repoMock.Verify(r => r.UpdateSubscriptionAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.CreateSubscriptionAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── EnsureStripeCustomerAsync ─────────────────────────────────────────────

    [Fact]
    public async Task EnsureStripeCustomer_WhenNotExists_CreatesNewRecord()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetStripeCustomerAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StripeCustomer?)null);
        _repoMock.Setup(r => r.CreateStripeCustomerAsync(It.IsAny<StripeCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeCustomer());

        await CreateSut().EnsureStripeCustomerAsync(userId, "cus_newstripe", "user@example.com");

        _repoMock.Verify(r => r.CreateStripeCustomerAsync(
            It.Is<StripeCustomer>(sc => sc.StripeCustomerId == "cus_newstripe" && sc.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureStripeCustomer_WhenAlreadyExists_IsNoOp()
    {
        var userId   = Guid.NewGuid();
        var existing = new StripeCustomer { StripeCustomerId = "cus_existing", UserId = userId };

        _repoMock.Setup(r => r.GetStripeCustomerAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await CreateSut().EnsureStripeCustomerAsync(userId, "cus_existing", "user@example.com");

        _repoMock.Verify(r => r.CreateStripeCustomerAsync(It.IsAny<StripeCustomer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── GetActiveMachinesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveMachines_DelegatesToRepository()
    {
        var licenceId = Guid.NewGuid();
        var machines  = new List<Machine>
        {
            new() { MachineId = Guid.NewGuid(), MachineName = "DESKTOP-A", IsActive = true, FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow },
            new() { MachineId = Guid.NewGuid(), MachineName = "LAPTOP-B",  IsActive = true, FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow }
        };

        _repoMock.Setup(r => r.GetActiveMachinesAsync(licenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machines);

        var result = await CreateSut().GetActiveMachinesAsync(licenceId);

        result.Should().HaveCount(2);
        result.Should().BeSameAs(machines);
    }

    [Fact]
    public async Task GetActiveMachines_WhenNoMachines_ReturnsEmptyList()
    {
        var licenceId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetActiveMachinesAsync(licenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Machine>());

        var result = await CreateSut().GetActiveMachinesAsync(licenceId);

        result.Should().BeEmpty();
    }

    // ── GetActiveLicenceAsync / GetSubscriptionAsync ──────────────────────────

    [Fact]
    public async Task GetActiveLicence_DelegatesToRepository()
    {
        var userId  = Guid.NewGuid();
        var licence = BuildLicence();
        _repoMock.Setup(r => r.GetActiveLicenceForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(licence);

        var result = await CreateSut().GetActiveLicenceAsync(userId);

        result.Should().BeSameAs(licence);
    }

    [Fact]
    public async Task GetSubscription_WhenNone_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetActiveSubscriptionForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        var result = await CreateSut().GetSubscriptionAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
