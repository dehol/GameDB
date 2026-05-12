namespace GameDB.Core.Interfaces;

public interface IAuditService
{
    Task LogActionAsync(
        int? userId,
        string actionType,
        string? entityId = null,
        object? oldValue = null,
        object? newValue = null,
        string? ipAddress = null,
        CancellationToken ct = default);
}
