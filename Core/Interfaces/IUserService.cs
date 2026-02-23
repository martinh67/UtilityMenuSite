using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IUserService
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken ct = default);
    Task<User?> GetByApiTokenAsync(string token, CancellationToken ct = default);
    Task<User> RegisterOrGetAsync(string email, CancellationToken ct = default);
    Task<User> RegisterFromIdentityAsync(string email, string identityId, CancellationToken ct = default);
    Task UpdateProfileAsync(Guid userId, string? displayName, string? organisation, string? jobRole, string? usageInterests, CancellationToken ct = default);
    Task<string> RegenerateApiTokenAsync(Guid userId, CancellationToken ct = default);
    Task<List<User>> SearchUsersAsync(string query, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<AdminStatsDto> GetAdminStatsAsync(CancellationToken ct = default);
    Task<User?> GetWithLicenceAsync(Guid userId, CancellationToken ct = default);
}
