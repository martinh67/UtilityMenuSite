using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Services.User;

namespace UtilityMenuSite.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository>    _userRepoMock    = new();
    private readonly Mock<ILicenceRepository> _licenceRepoMock = new();
    private readonly Mock<IContactRepository> _contactRepoMock = new();
    private readonly Mock<ILogger<UserService>> _loggerMock    = new();

    private UserService CreateSut() =>
        new(_userRepoMock.Object, _licenceRepoMock.Object, _contactRepoMock.Object, _loggerMock.Object);

    // ── RegisterFromIdentityAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RegisterFromIdentityAsync_WhenUserDoesNotExist_CreatesNewUser()
    {
        // No existing user for this email
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var sut = CreateSut();
        var result = await sut.RegisterFromIdentityAsync("new@example.com", "identity-id-123");

        result.Email.Should().Be("new@example.com");
        result.IdentityId.Should().Be("identity-id-123");
        result.ApiToken.Should().NotBeNullOrWhiteSpace();
        result.IsActive.Should().BeTrue();

        _userRepoMock.Verify(r => r.CreateAsync(
            It.Is<User>(u => u.Email == "new@example.com" && u.IdentityId == "identity-id-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterFromIdentityAsync_WhenUserExistsWithNoIdentityId_LinksIdentityId()
    {
        // User was created via Stripe checkout — has no IdentityId yet
        var existing = new User
        {
            UserId    = Guid.NewGuid(),
            Email     = "existing@example.com",
            IdentityId = null,
            ApiToken  = "existing-token",
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userRepoMock
            .Setup(r => r.GetByEmailAsync("existing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var sut = CreateSut();
        var result = await sut.RegisterFromIdentityAsync("existing@example.com", "new-identity-id");

        result.IdentityId.Should().Be("new-identity-id");

        _userRepoMock.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.IdentityId == "new-identity-id"),
            It.IsAny<CancellationToken>()), Times.Once);

        _userRepoMock.Verify(r => r.CreateAsync(
            It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterFromIdentityAsync_WhenUserExistsWithIdentityId_ReturnsExistingWithoutUpdate()
    {
        // User already fully registered — nothing should change
        var existing = new User
        {
            UserId     = Guid.NewGuid(),
            Email      = "linked@example.com",
            IdentityId = "already-linked-id",
            ApiToken   = "existing-token",
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow
        };

        _userRepoMock
            .Setup(r => r.GetByEmailAsync("linked@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var sut = CreateSut();
        var result = await sut.RegisterFromIdentityAsync("linked@example.com", "already-linked-id");

        result.Should().BeSameAs(existing);

        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepoMock.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterFromIdentityAsync_NewUser_GeneratesNonEmptyApiToken()
    {
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var sut = CreateSut();
        var result = await sut.RegisterFromIdentityAsync("token@example.com", "id");

        result.ApiToken.Should().HaveLength(64);
    }

    [Fact]
    public async Task RegisterFromIdentityAsync_NewUser_SetsIsActiveTrue()
    {
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var sut = CreateSut();
        var result = await sut.RegisterFromIdentityAsync("active@example.com", "id");

        result.IsActive.Should().BeTrue();
    }

    // ── RegenerateApiTokenAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RegenerateApiTokenAsync_WhenUserExists_ReturnsNewToken()
    {
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, ApiToken = "old-token", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = CreateSut();
        var newToken = await sut.RegenerateApiTokenAsync(userId);

        newToken.Should().NotBe("old-token");
        newToken.Should().HaveLength(64);
        user.ApiToken.Should().Be(newToken);

        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegenerateApiTokenAsync_WhenUserNotFound_Throws()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = CreateSut();

        await sut.Invoking(s => s.RegenerateApiTokenAsync(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // ── GetByApiTokenAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetByApiTokenAsync_WhenTokenExists_ReturnsUser()
    {
        var user = new User { UserId = Guid.NewGuid(), ApiToken = "valid-token" };
        _userRepoMock.Setup(r => r.GetByApiTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await CreateSut().GetByApiTokenAsync("valid-token");

        result.Should().BeSameAs(user);
    }

    [Fact]
    public async Task GetByApiTokenAsync_WhenTokenNotFound_ReturnsNull()
    {
        _userRepoMock.Setup(r => r.GetByApiTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().GetByApiTokenAsync("bad-token");

        result.Should().BeNull();
    }
}
