using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Data.Context;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AppDbContext db, ILogger<AuditLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string? entityName = null,
        string? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        Guid? userId = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            UserId = userId,
            IpAddress = ipAddress,
            OccurredAt = DateTime.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit logging failures must never surface to the caller.
            _logger.LogError(ex, "Failed to write audit log entry: action={Action} entity={Entity}/{Id}",
                action, entityName, entityId);
        }
    }
}
