using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Core.Interfaces;

public interface IContactService
{
    Task<bool> SubmitAsync(ContactFormDto dto, string? ipAddress, CancellationToken ct = default);
    Task<List<ContactSubmission>> GetPendingSubmissionsAsync(CancellationToken ct = default);
    Task<bool> ResolveAsync(Guid submissionId, string? notes, CancellationToken ct = default);
}
