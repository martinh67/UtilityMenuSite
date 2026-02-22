using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Services.Contact;

public class ContactService : IContactService
{
    private readonly IContactRepository _contactRepo;
    private readonly ILogger<ContactService> _logger;

    public ContactService(IContactRepository contactRepo, ILogger<ContactService> logger)
    {
        _contactRepo = contactRepo;
        _logger = logger;
    }

    public async Task<bool> SubmitAsync(ContactFormDto dto, string? ipAddress, CancellationToken ct = default)
    {
        // Honeypot check
        if (!string.IsNullOrWhiteSpace(dto.Website))
        {
            _logger.LogWarning("Honeypot triggered from IP {IP}", ipAddress);
            return true; // Return true to not reveal the check
        }

        // Rate limit: max 3 per IP per hour
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            var since = DateTime.UtcNow.AddHours(-1);
            var count = await _contactRepo.CountByIpSinceAsync(ipAddress, since, ct);
            if (count >= 3)
            {
                _logger.LogWarning("Rate limit exceeded for contact from IP {IP}", ipAddress);
                return false;
            }
        }

        var submission = new ContactSubmission
        {
            Name = dto.Name,
            Email = dto.Email,
            Subject = dto.Subject,
            Message = dto.Message,
            IpAddress = ipAddress,
            SubmittedAt = DateTime.UtcNow
        };

        await _contactRepo.CreateAsync(submission, ct);
        _logger.LogInformation("Contact submission from {Email}: {Subject}", dto.Email, dto.Subject);

        return true;
    }

    public async Task<List<ContactSubmission>> GetPendingSubmissionsAsync(CancellationToken ct = default)
        => await _contactRepo.GetPendingAsync(ct);

    public async Task<bool> ResolveAsync(Guid submissionId, string? notes, CancellationToken ct = default)
    {
        var submission = await _contactRepo.GetByIdAsync(submissionId, ct);
        if (submission is null) return false;

        submission.IsResolved = true;
        submission.ResolvedAt = DateTime.UtcNow;
        submission.Notes = notes;
        await _contactRepo.UpdateAsync(submission, ct);

        return true;
    }
}
