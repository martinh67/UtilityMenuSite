namespace UtilityMenuSite.Core.Interfaces;

public interface IAuditLogService
{
    /// <summary>
    /// Records an admin-facing audit log entry. All parameters except <paramref name="action"/>
    /// are optional. Non-nullable values are stored as strings.
    /// </summary>
    Task LogAsync(
        string action,
        string? entityName = null,
        string? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        Guid? userId = null,
        string? ipAddress = null,
        CancellationToken ct = default);
}
