using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken ct = default);
    Task<User?> GetByApiTokenAsync(string token, CancellationToken ct = default);
    Task<List<User>> SearchAsync(string query, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<User> CreateAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<List<User>> GetRecentAsync(int count, CancellationToken ct = default);
    Task<User?> GetWithLicenceAsync(Guid userId, CancellationToken ct = default);
}
