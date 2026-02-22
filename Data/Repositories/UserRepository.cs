using Microsoft.EntityFrameworkCore;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => await _db.AppUsers.FindAsync([userId], ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken ct = default)
        => await _db.AppUsers.FirstOrDefaultAsync(u => u.IdentityId == identityId, ct);

    public async Task<User?> GetByApiTokenAsync(string token, CancellationToken ct = default)
        => await _db.AppUsers.FirstOrDefaultAsync(u => u.ApiToken == token, ct);

    public async Task<List<User>> SearchAsync(string query, int skip = 0, int take = 20, CancellationToken ct = default)
        => await _db.AppUsers
            .Where(u => u.Email.Contains(query) || (u.DisplayName != null && u.DisplayName.Contains(query)))
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _db.AppUsers.Update(user);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await _db.AppUsers.CountAsync(ct);

    public async Task<List<User>> GetRecentAsync(int count, CancellationToken ct = default)
        => await _db.AppUsers
            .OrderByDescending(u => u.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<User?> GetWithLicenceAsync(Guid userId, CancellationToken ct = default)
        => await _db.AppUsers
            .Include(u => u.Licences.Where(l => l.IsActive))
                .ThenInclude(l => l.LicenceModules)
                    .ThenInclude(lm => lm.Module)
            .Include(u => u.StripeCustomer)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);
}
