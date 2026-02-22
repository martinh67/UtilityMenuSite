using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Services.Licensing;

namespace UtilityMenuSite.Tests.Services;

public class LicenceServiceTests
{
    private readonly Mock<ILicenceRepository> _repoMock = new();
    private readonly LicensingSettings _settings = new()
    {
        HmacSigningKey = "test-hmac-key-that-is-long-enough-for-sha256",
        StalenessWindowDays = 7,
        GracePeriodDays = 7
    };

    private LicenceService CreateSut() =>
        new(_repoMock.Object, Options.Create(_settings));

    // ── ValidateLicenceAsync ────────────────────────────────

    [Fact]
    public async Task ValidateLicence_WhenKeyNotFound_ReturnsInvalid()
    {
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Licence?)null);

        var sut = CreateSut();
        var result = await sut.ValidateLicenceAsync("UMENU-XXXX-XXXX-XXXX", CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("Licence not found");
    }

    [Fact]
    public async Task ValidateLicence_WhenRevoked_ReturnsInvalid()
    {
        var licence = BuildLicence(isActive: false);
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var result = await CreateSut().ValidateLicenceAsync(licence.LicenceKey, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("Licence is not active");
    }

    [Fact]
    public async Task ValidateLicence_WhenExpired_ReturnsInvalid()
    {
        var licence = BuildLicence(expiresAt: DateTime.UtcNow.AddDays(-1));
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var result = await CreateSut().ValidateLicenceAsync(licence.LicenceKey, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("Licence has expired");
    }

    [Fact]
    public async Task ValidateLicence_WhenValid_ReturnsValid()
    {
        var licence = BuildLicence();
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var result = await CreateSut().ValidateLicenceAsync(licence.LicenceKey, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.LicenceType.Should().Be(licence.LicenceType);
        result.ExpiresAt.Should().Be(licence.ExpiresAt);
    }

    // ── GetEntitlementsAsync ────────────────────────────────

    [Fact]
    public async Task GetEntitlements_WhenValid_IncludesModulesAndSignature()
    {
        var licence = BuildLicence();
        licence.LicenceModules =
        [
            new LicenceModule { Module = new Module { Name = "GetLastRow" } },
            new LicenceModule { Module = new Module { Name = "GetLastColumn" } }
        ];

        _repoMock.Setup(r => r.GetByKeyWithModulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var result = await CreateSut().GetEntitlementsAsync(licence.LicenceKey, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Modules.Should().Contain("GetLastRow");
        result.Signature.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetEntitlements_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByKeyWithModulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Licence?)null);

        var result = await CreateSut().GetEntitlementsAsync("UMENU-FAKE-FAKE-FAKE", CancellationToken.None);

        result.Should().BeNull();
    }

    // ── ActivateMachineAsync ────────────────────────────────

    [Fact]
    public async Task ActivateMachine_WhenSeatLimitExceeded_ThrowsSeatLimitExceededException()
    {
        var licence = BuildLicence(maxActivations: 1);
        licence.Machines = [new Machine { IsActive = true }];

        _repoMock.Setup(r => r.GetByKeyWithMachinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var request = new ActivateMachineRequest
        {
            LicenceKey = licence.LicenceKey,
            MachineId = Guid.NewGuid(),
            MachineName = "NEW-MACHINE"
        };

        await CreateSut()
            .Invoking(s => s.ActivateMachineAsync(request, CancellationToken.None))
            .Should().ThrowAsync<SeatLimitExceededException>();
    }

    [Fact]
    public async Task ActivateMachine_WhenAlreadyActivated_ReturnsExistingMachine()
    {
        var machineId = Guid.NewGuid();
        var licence = BuildLicence(maxActivations: 3);
        var existingMachine = new Machine
        {
            Id = machineId,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow.AddDays(-5)
        };
        licence.Machines = [existingMachine];

        _repoMock.Setup(r => r.GetByKeyWithMachinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var request = new ActivateMachineRequest
        {
            LicenceKey = licence.LicenceKey,
            MachineId = machineId,
            MachineName = "SAME-MACHINE"
        };

        var result = await CreateSut().ActivateMachineAsync(request, CancellationToken.None);

        result.MachineId.Should().Be(machineId);
        _repoMock.Verify(r => r.AddMachineAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateMachine_WhenSlotAvailable_AddsNewMachine()
    {
        var licence = BuildLicence(maxActivations: 3);
        licence.Machines = [];

        _repoMock.Setup(r => r.GetByKeyWithMachinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);
        _repoMock.Setup(r => r.AddMachineAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var request = new ActivateMachineRequest
        {
            LicenceKey = licence.LicenceKey,
            MachineId = Guid.NewGuid(),
            MachineName = "DESKTOP-NEW"
        };

        var result = await CreateSut().ActivateMachineAsync(request, CancellationToken.None);

        result.ActiveCount.Should().Be(1);
        result.MaxActivations.Should().Be(3);
        _repoMock.Verify(r => r.AddMachineAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ────────────────────────────────────────────

    private static Licence BuildLicence(
        bool isActive = true,
        DateTime? expiresAt = null,
        int maxActivations = 3)
    {
        return new Licence
        {
            Id = Guid.NewGuid(),
            LicenceKey = "UMENU-TEST-TEST-TEST",
            LicenceType = "pro",
            IsActive = isActive,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddYears(1),
            MaxActivations = maxActivations,
            Machines = [],
            LicenceModules = []
        };
    }
}
