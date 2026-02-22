using Microsoft.EntityFrameworkCore;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly AppDbContext _db;

    public ContactRepository(AppDbContext db) => _db = db;

    public async Task<ContactSubmission> CreateAsync(ContactSubmission submission, CancellationToken ct = default)
    {
        _db.ContactSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);
        return submission;
    }

    public async Task<List<ContactSubmission>> GetPendingAsync(CancellationToken ct = default)
        => await _db.ContactSubmissions
            .Where(c => !c.IsResolved)
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync(ct);

    public async Task<ContactSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.ContactSubmissions.FindAsync([id], ct);

    public async Task UpdateAsync(ContactSubmission submission, CancellationToken ct = default)
    {
        _db.ContactSubmissions.Update(submission);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CountByIpSinceAsync(string ipAddress, DateTime since, CancellationToken ct = default)
        => await _db.ContactSubmissions
            .CountAsync(c => c.IpAddress == ipAddress && c.SubmittedAt >= since, ct);
}
