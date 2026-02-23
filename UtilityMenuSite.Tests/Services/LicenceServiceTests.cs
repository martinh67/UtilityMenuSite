using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Configuration;
using UtilityMenuSite.Services.Licensing;

namespace UtilityMenuSite.Tests.Services;

public class LicenceServiceTests
{
    private readonly Mock<ILicenceRepository> _repoMock = new();
    private readonly Mock<ILogger<LicenceService>> _loggerMock = new();
    private readonly LicensingSettings _settings = new()
    {
        HmacSigningKey = "dGVzdC1obWFjLWtleS10aGF0LWlzLWxvbmctZW5vdWdoLWZvci1zaGEyNTY=",
        StalenessWindowDays = 7,
        GracePeriodDays = 7
    };

    private LicenceService CreateSut() =>
        new(_repoMock.Object, Options.Create(_settings), _loggerMock.Object);

    // ── ValidateLicenceAsync ────────────────────────────────

    [Fact]
    public async Task ValidateLicence_WhenKeyNotFound_ReturnsInvalid()
    {
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Licence?)null);

        var sut = CreateSut();
        var result = await sut.ValidateLicenceAsync("UMENU-XXXX-XXXX-XXXX", CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("not_found");
    }

    [Fact]
    public async Task ValidateLicence_WhenRevoked_ReturnsInvalid()
    {
        var licence = BuildLicence(isActive: false);
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var result = await CreateSut().ValidateLicenceAsync(licence.LicenceKey, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("inactive");
    }

    [Fact]
    public async Task ValidateLicence_WhenExpired_ReturnsInvalid()
    {
        var licence = BuildLicence(expiresAt: DateTime.UtcNow.AddDays(-1));
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var result = await CreateSut().ValidateLicenceAsync(licence.LicenceKey, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("expired");
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
            new LicenceModule { Module = new Module { ModuleName = "GetLastRow", IsActive = true } },
            new LicenceModule { Module = new Module { ModuleName = "GetLastColumn", IsActive = true } }
        ];

        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);

        var result = await CreateSut().GetEntitlementsAsync(licence.LicenceKey, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Modules.Should().Contain("GetLastRow");
        result.Signature.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetEntitlements_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Licence?)null);

        var result = await CreateSut().GetEntitlementsAsync("UMENU-FAKE-FAKE-FAKE", CancellationToken.None);

        result.Should().BeNull();
    }

    // ── ActivateMachineAsync ────────────────────────────────

    [Fact]
    public async Task ActivateMachine_WhenSeatLimitExceeded_ThrowsSeatLimitExceededException()
    {
        var licence = BuildLicence(maxActivations: 1);

        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);
        _repoMock.Setup(r => r.GetMachineAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Machine?)null);
        _repoMock.Setup(r => r.GetActiveMachineCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(1);

        var request = new ActivateMachineRequest
        {
            LicenceKey = licence.LicenceKey,
            MachineFingerprint = "new-machine-fingerprint",
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
            MachineId = machineId,
            IsActive = true,
            FirstSeenAt = DateTime.UtcNow.AddDays(-5)
        };

        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);
        _repoMock.Setup(r => r.GetMachineAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existingMachine);
        _repoMock.Setup(r => r.GetActiveMachineCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(1);

        var request = new ActivateMachineRequest
        {
            LicenceKey = licence.LicenceKey,
            MachineFingerprint = "existing-fingerprint",
            MachineName = "SAME-MACHINE"
        };

        var result = await CreateSut().ActivateMachineAsync(request, CancellationToken.None);

        result.MachineId.Should().Be(machineId);
        _repoMock.Verify(r => r.CreateMachineAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateMachine_WhenSlotAvailable_AddsNewMachine()
    {
        var licence = BuildLicence(maxActivations: 3);

        _repoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(licence);
        _repoMock.Setup(r => r.GetMachineAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Machine?)null);
        _repoMock.Setup(r => r.GetActiveMachineCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(0);
        _repoMock.Setup(r => r.CreateMachineAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new Machine());

        var request = new ActivateMachineRequest
        {
            LicenceKey = licence.LicenceKey,
            MachineFingerprint = "new-machine-fingerprint",
            MachineName = "DESKTOP-NEW"
        };

        var result = await CreateSut().ActivateMachineAsync(request, CancellationToken.None);

        result.ActiveCount.Should().Be(1);
        result.MaxActivations.Should().Be(3);
        _repoMock.Verify(r => r.CreateMachineAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ────────────────────────────────────────────

    private static Licence BuildLicence(
        bool isActive = true,
        DateTime? expiresAt = null,
        int maxActivations = 3)
    {
        return new Licence
        {
            LicenceId = Guid.NewGuid(),
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
