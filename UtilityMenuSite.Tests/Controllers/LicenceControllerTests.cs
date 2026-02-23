using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UtilityMenuSite.Controllers;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Tests.Controllers;

public class LicenceControllerTests
{
    private readonly Mock<ILicenceService> _licenceServiceMock = new();
    private readonly Mock<IUserService>    _userServiceMock    = new();
    private readonly Mock<ILogger<LicenceController>> _loggerMock = new();

    private LicenceController CreateSut(string? bearerToken = null)
    {
        var controller = new LicenceController(
            _licenceServiceMock.Object,
            _userServiceMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        if (bearerToken is not null)
            httpContext.Request.Headers.Authorization = $"Bearer {bearerToken}";

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    // ── Validate ────────────────────────────────────────────

    [Fact]
    public async Task Validate_WithEmptyKey_ReturnsBadRequest()
    {
        var sut = CreateSut();
        var result = await sut.Validate("", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Validate_WhenInvalid_ReturnsOkWithIsValidFalse()
    {
        _licenceServiceMock
            .Setup(s => s.ValidateLicenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenceValidationResult { IsValid = false, Reason = "Expired" });

        var result = await CreateSut().Validate("UMENU-FAKE-FAKE-FAKE", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!.GetType().GetProperty("isValid")!.GetValue(ok.Value);
        body.Should().Be(false);
    }

    [Fact]
    public async Task Validate_WhenValid_ReturnsOkWithDetails()
    {
        var expiry = DateTime.UtcNow.AddYears(1);
        _licenceServiceMock
            .Setup(s => s.ValidateLicenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenceValidationResult
            {
                IsValid = true,
                LicenceType = "pro",
                ExpiresAt = expiry
            });

        var result = await CreateSut().Validate("UMENU-TEST-TEST-TEST", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var type = ok.Value!.GetType().GetProperty("licenceType")!.GetValue(ok.Value);
        type.Should().Be("pro");
    }

    // ── Entitlements ────────────────────────────────────────

    [Fact]
    public async Task Entitlements_WithEmptyKey_ReturnsBadRequest()
    {
        var result = await CreateSut().Entitlements("  ", CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Entitlements_WhenNotFound_ReturnsNotFound()
    {
        _licenceServiceMock
            .Setup(s => s.GetEntitlementsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LicenceEntitlementsResult?)null);

        var result = await CreateSut().Entitlements("UMENU-NONE-NONE-NONE", CancellationToken.None);
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Entitlements_WhenFound_ReturnsOkWithModules()
    {
        _licenceServiceMock
            .Setup(s => s.GetEntitlementsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenceEntitlementsResult
            {
                IsValid = true,
                LicenceKey = "UMENU-TEST-TEST-TEST",
                LicenceType = "pro",
                Modules = ["GetLastRow", "AdvancedData"],
                Signature = "abc123"
            });

        var result = await CreateSut().Entitlements("UMENU-TEST-TEST-TEST", CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    // ── Activate ────────────────────────────────────────────

    [Fact]
    public async Task Activate_WithoutBearerToken_ReturnsUnauthorized()
    {
        var request = new ActivateMachineRequest
        {
            LicenceKey = "UMENU-TEST-TEST-TEST",
            MachineId = Guid.NewGuid(),
            MachineName = "MACHINE"
        };

        var result = await CreateSut(bearerToken: null).Activate(request, CancellationToken.None);
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Activate_WithInvalidToken_ReturnsUnauthorized()
    {
        _userServiceMock
            .Setup(u => u.GetByApiTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new ActivateMachineRequest
        {
            LicenceKey = "UMENU-TEST-TEST-TEST",
            MachineId = Guid.NewGuid(),
            MachineName = "MACHINE"
        };

        var result = await CreateSut(bearerToken: "bad-token").Activate(request, CancellationToken.None);
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Activate_WhenSeatLimitExceeded_ReturnsBadRequest()
    {
        _userServiceMock
            .Setup(u => u.GetByApiTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid() });

        _licenceServiceMock
            .Setup(s => s.ActivateMachineAsync(It.IsAny<ActivateMachineRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SeatLimitExceededException("Seat limit exceeded"));

        var request = new ActivateMachineRequest
        {
            LicenceKey = "UMENU-TEST-TEST-TEST",
            MachineId = Guid.NewGuid(),
            MachineName = "MACHINE"
        };

        var result = await CreateSut(bearerToken: "valid-token").Activate(request, CancellationToken.None);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var code = bad.Value!.GetType().GetProperty("code")!.GetValue(bad.Value);
        code.Should().Be("SEAT_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task Activate_WhenSuccessful_ReturnsOkWithMachineDetails()
    {
        var machineId = Guid.NewGuid();

        _userServiceMock
            .Setup(u => u.GetByApiTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid() });

        _licenceServiceMock
            .Setup(s => s.ActivateMachineAsync(It.IsAny<ActivateMachineRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivateMachineResult
            {
                MachineId = machineId,
                ActivatedAt = DateTime.UtcNow,
                ActiveCount = 1,
                MaxActivations = 3
            });

        var request = new ActivateMachineRequest
        {
            LicenceKey = "UMENU-TEST-TEST-TEST",
            MachineId = machineId,
            MachineName = "MACHINE"
        };

        var result = await CreateSut(bearerToken: "valid-token").Activate(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var mid = ok.Value!.GetType().GetProperty("machineId")!.GetValue(ok.Value);
        mid.Should().Be(machineId);
    }
}
