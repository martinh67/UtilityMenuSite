using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Security;

namespace UtilityMenuSite.Services.User;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly ILicenceRepository _licenceRepo;
    private readonly IContactRepository _contactRepo;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepo,
        ILicenceRepository licenceRepo,
        IContactRepository contactRepo,
        ILogger<UserService> logger)
    {
        _userRepo = userRepo;
        _licenceRepo = licenceRepo;
        _contactRepo = contactRepo;
        _logger = logger;
    }

    public async Task<Data.Models.User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => await _userRepo.GetByIdAsync(userId, ct);

    public async Task<Data.Models.User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _userRepo.GetByEmailAsync(email, ct);

    public async Task<Data.Models.User?> GetByIdentityIdAsync(string identityId, CancellationToken ct = default)
        => await _userRepo.GetByIdentityIdAsync(identityId, ct);

    public async Task<Data.Models.User?> GetByApiTokenAsync(string token, CancellationToken ct = default)
        => await _userRepo.GetByApiTokenAsync(token, ct);

    public async Task<Data.Models.User> RegisterOrGetAsync(string email, CancellationToken ct = default)
    {
        var existing = await _userRepo.GetByEmailAsync(email, ct);
        if (existing is not null) return existing;

        var user = new Data.Models.User
        {
            Email = email,
            ApiToken = ApiTokenGenerator.Generate(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepo.CreateAsync(user, ct);
        _logger.LogInformation("Registered new user {Email} via checkout", email);

        return user;
    }

    public async Task<Data.Models.User> RegisterFromIdentityAsync(string email, string identityId, CancellationToken ct = default)
    {
        var existing = await _userRepo.GetByEmailAsync(email, ct);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.IdentityId))
            {
                existing.IdentityId = identityId;
                await _userRepo.UpdateAsync(existing, ct);
            }
            return existing;
        }

        var user = new Data.Models.User
        {
            Email = email,
            IdentityId = identityId,
            ApiToken = ApiTokenGenerator.Generate(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepo.CreateAsync(user, ct);
        _logger.LogInformation("Registered new user {Email} from identity provider", email);
        return user;
    }

    public async Task UpdateProfileAsync(Guid userId, string? displayName, string? organisation, string? jobRole, string? usageInterests, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null) throw new InvalidOperationException("User not found");

        user.DisplayName = displayName;
        user.Organisation = organisation;
        user.JobRole = jobRole;
        user.UsageInterests = usageInterests;
        user.ProfileCompletedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);

        _logger.LogInformation("Updated profile for user {UserId}", userId);
    }

    public async Task<string> RegenerateApiTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null) throw new InvalidOperationException("User not found");

        var newToken = ApiTokenGenerator.Generate();
        user.ApiToken = newToken;
        await _userRepo.UpdateAsync(user, ct);

        _logger.LogInformation("Regenerated API token for user {UserId}", userId);
        return newToken;
    }

    public async Task<List<Data.Models.User>> SearchUsersAsync(string query, int skip = 0, int take = 20, CancellationToken ct = default)
        => await _userRepo.SearchAsync(query, skip, take, ct);

    public async Task<AdminStatsDto> GetAdminStatsAsync(CancellationToken ct = default)
    {
        var totalUsers = await _userRepo.GetTotalCountAsync(ct);
        var activeLicences = await _licenceRepo.GetTotalActiveLicencesAsync(ct);
        var recentSignups = (await _userRepo.GetRecentAsync(7, ct)).Count(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-7));
        var pendingContacts = (await _contactRepo.GetPendingAsync(ct)).Count;
        var failedWebhooks = (await _licenceRepo.GetFailedWebhookEventsAsync(ct)).Count;

        return new AdminStatsDto
        {
            TotalUsers = totalUsers,
            ActiveLicences = activeLicences,
            RecentSignups = recentSignups,
            PendingContactSubmissions = pendingContacts,
            FailedWebhookEvents = failedWebhooks
        };
    }

    public async Task<Data.Models.User?> GetWithLicenceAsync(Guid userId, CancellationToken ct = default)
        => await _userRepo.GetWithLicenceAsync(userId, ct);
}
