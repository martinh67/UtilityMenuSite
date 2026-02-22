using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IContactRepository
{
    Task<ContactSubmission> CreateAsync(ContactSubmission submission, CancellationToken ct = default);
    Task<List<ContactSubmission>> GetPendingAsync(CancellationToken ct = default);
    Task<ContactSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(ContactSubmission submission, CancellationToken ct = default);
    Task<int> CountByIpSinceAsync(string ipAddress, DateTime since, CancellationToken ct = default);
}
