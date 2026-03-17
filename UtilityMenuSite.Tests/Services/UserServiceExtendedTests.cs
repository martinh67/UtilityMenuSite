using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Services.User;

namespace UtilityMenuSite.Tests.Services;

/// <summary>
/// Unit tests for the remaining UserService methods not covered by UserServiceTests.cs:
/// RegisterOrGetAsync, UpdateProfileAsync, GetAdminStatsAsync, delegation methods.
/// </summary>
public class UserServiceExtendedTests
{
    private readonly Mock<IUserRepository>     _userRepoMock    = new();
    private readonly Mock<ILicenceRepository>  _licenceRepoMock = new();
    private readonly Mock<IContactRepository>  _contactRepoMock = new();
    private readonly Mock<ILogger<UserService>> _loggerMock     = new();

    private UserService CreateSut() =>
        new(_userRepoMock.Object, _licenceRepoMock.Object, _contactRepoMock.Object, _loggerMock.Object);

    // ── RegisterOrGetAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterOrGetAsync_WhenUserDoesNotExist_CreatesNewUser()
    {
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var result = await CreateSut().RegisterOrGetAsync("checkout@example.com");

        result.Email.Should().Be("checkout@example.com");
        result.ApiToken.Should().NotBeNullOrWhiteSpace();
        result.IsActive.Should().BeTrue();
        result.IdentityId.Should().BeNull("checkout-created users have no Identity account yet");

        _userRepoMock.Verify(r => r.CreateAsync(
            It.Is<User>(u => u.Email == "checkout@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterOrGetAsync_WhenUserAlreadyExists_ReturnsExistingWithoutCreate()
    {
        var existing = new User
        {
            UserId   = Guid.NewGuid(),
            Email    = "existing@example.com",
            ApiToken = "existing-token",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userRepoMock
            .Setup(r => r.GetByEmailAsync("existing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateSut().RegisterOrGetAsync("existing@example.com");

        result.Should().BeSameAs(existing);
        _userRepoMock.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterOrGetAsync_NewUser_GeneratesNonEmptyApiToken()
    {
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var result = await CreateSut().RegisterOrGetAsync("newcheckout@example.com");

        result.ApiToken.Should().HaveLength(64);
    }

    [Fact]
    public async Task RegisterOrGetAsync_CalledTwiceWithSameEmail_SecondCallReturnsFirst()
    {
        // Simulates Stripe sending duplicate webhook events — idempotency check.
        User? stored = null;

        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);

        _userRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => stored = u)
            .ReturnsAsync((User u, CancellationToken _) => u);

        var sut = CreateSut();
        var first  = await sut.RegisterOrGetAsync("idempotent@example.com");
        var second = await sut.RegisterOrGetAsync("idempotent@example.com");

        second.Should().BeSameAs(first);
        _userRepoMock.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateProfileAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfileAsync_WhenUserExists_UpdatesAllFields()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId    = userId,
            Email     = "profile@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await CreateSut().UpdateProfileAsync(
            userId,
            displayName:    "Martin Hannah",
            organisation:   "Acme Ltd",
            jobRole:        "Developer",
            usageInterests: "Data cleaning,Reporting");

        user.DisplayName.Should().Be("Martin Hannah");
        user.Organisation.Should().Be("Acme Ltd");
        user.JobRole.Should().Be("Developer");
        user.UsageInterests.Should().Be("Data cleaning,Reporting");
        user.ProfileCompletedAt.Should().NotBeNull();

        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_SetsProfileCompletedAtTimestamp()
    {
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, ProfileCompletedAt = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var before = DateTime.UtcNow;
        await CreateSut().UpdateProfileAsync(userId, "Name", null, null, null);
        var after = DateTime.UtcNow;

        user.ProfileCompletedAt.Should().NotBeNull();
        user.ProfileCompletedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenUserNotFound_ThrowsInvalidOperationException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = CreateSut();

        await sut.Invoking(s => s.UpdateProfileAsync(Guid.NewGuid(), "Name", null, null, null))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateProfileAsync_WithNullFields_AcceptsNullValues()
    {
        // CompleteProfile has a "skip" path that can pass all nulls.
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId      = userId,
            DisplayName = "Existing Name",
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act — all optional fields null
        await CreateSut().UpdateProfileAsync(userId, null, null, null, null);

        user.DisplayName.Should().BeNull();
        user.ProfileCompletedAt.Should().NotBeNull();
        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetAdminStatsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminStatsAsync_AggregatesFromAllRepositories()
    {
        var recentUsers = new List<User>
        {
            new() { UserId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new() { UserId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddHours(-2) }
        };

        _userRepoMock.Setup(r => r.GetTotalCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        _userRepoMock.Setup(r => r.GetRecentAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentUsers);

        _licenceRepoMock.Setup(r => r.GetTotalActiveLicencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);
        _licenceRepoMock.Setup(r => r.GetFailedWebhookEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UtilityMenuSite.Data.Models.StripeWebhookEvent> { new(), new() });

        _contactRepoMock.Setup(r => r.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UtilityMenuSite.Data.Models.ContactSubmission> { new() });

        var result = await CreateSut().GetAdminStatsAsync();

        result.TotalUsers.Should().Be(42);
        result.ActiveLicences.Should().Be(15);
        result.RecentSignups.Should().Be(2);
        result.PendingContactSubmissions.Should().Be(1);
        result.FailedWebhookEvents.Should().Be(2);
    }

    [Fact]
    public async Task GetAdminStatsAsync_WhenNoRecentSignups_ReturnsZero()
    {
        _userRepoMock.Setup(r => r.GetTotalCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _userRepoMock.Setup(r => r.GetRecentAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());  // empty — no signups in last 7 days
        _licenceRepoMock.Setup(r => r.GetTotalActiveLicencesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _licenceRepoMock.Setup(r => r.GetFailedWebhookEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UtilityMenuSite.Data.Models.StripeWebhookEvent>());
        _contactRepoMock.Setup(r => r.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UtilityMenuSite.Data.Models.ContactSubmission>());

        var result = await CreateSut().GetAdminStatsAsync();

        result.RecentSignups.Should().Be(0);
    }

    // ── Simple delegation tests ───────────────────────────────────────────────

    [Fact]
    public async Task GetByIdentityIdAsync_DelegatesToRepository()
    {
        var user = new User { UserId = Guid.NewGuid(), IdentityId = "identity-abc" };
        _userRepoMock.Setup(r => r.GetByIdentityIdAsync("identity-abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await CreateSut().GetByIdentityIdAsync("identity-abc");

        result.Should().BeSameAs(user);
    }

    [Fact]
    public async Task GetByEmailAsync_WhenNotFound_ReturnsNull()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().GetByEmailAsync("nope@example.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchUsersAsync_DelegatesToRepository()
    {
        var users = new List<User> { new() { Email = "admin@example.com" } };
        _userRepoMock.Setup(r => r.SearchAsync("admin", 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await CreateSut().SearchUsersAsync("admin");

        result.Should().BeEquivalentTo(users);
    }
}
