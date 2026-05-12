using System.Text.Json;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;

namespace GameDB.Infrastructure.Services;

public class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogActionAsync(
        int? userId,
        string actionType,
        string? entityId = null,
        object? oldValue = null,
        object? newValue = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            ActionType = actionType,
            EntityId = entityId,
            OldValue = oldValue == null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewValue = newValue == null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            IPAddress = ipAddress
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}
