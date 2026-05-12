using System.Security.Claims;
using System.Text.Json;
using GameDB.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameDB.Infrastructure.Services;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditLogs(DbContext? context)
    {
        if (context == null) return;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true || !httpContext.User.IsInRole("admin"))
            return;

        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : null;
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        var entries = context.ChangeTracker.Entries()
            .Where(e =>
                e.Entity is not AuditLog &&
                e.Entity is not ImportJobLog &&
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var (oldValue, newValue) = BuildPayload(entry);
            var log = new AuditLog
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                ActionType = $"{entry.Metadata.ClrType.Name}.{entry.State}",
                EntityId = GetEntityId(entry),
                OldValue = oldValue == null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
                NewValue = newValue == null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
                IPAddress = ipAddress
            };

            context.Set<AuditLog>().Add(log);
        }
    }

    private static string? GetEntityId(EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk == null) return null;

        var values = pk.Properties
            .Select(p => entry.Property(p.Name).CurrentValue ?? entry.Property(p.Name).OriginalValue)
            .Where(v => v != null)
            .Select(v => v!.ToString())
            .ToArray();

        return values.Length == 0 ? null : string.Join(":", values);
    }

    private static (Dictionary<string, object?>? oldValue, Dictionary<string, object?>? newValue)
        BuildPayload(EntityEntry entry)
    {
        Dictionary<string, object?>? oldValue = null;
        Dictionary<string, object?>? newValue = null;

        if (entry.State == EntityState.Added)
        {
            newValue = entry.CurrentValues.Properties.ToDictionary(p => p.Name, p => entry.CurrentValues[p]);
        }
        else if (entry.State == EntityState.Deleted)
        {
            oldValue = entry.OriginalValues.Properties.ToDictionary(p => p.Name, p => entry.OriginalValues[p]);
        }
        else if (entry.State == EntityState.Modified)
        {
            oldValue = new Dictionary<string, object?>();
            newValue = new Dictionary<string, object?>();

            foreach (var prop in entry.Properties.Where(p => p.IsModified))
            {
                oldValue[prop.Metadata.Name] = prop.OriginalValue;
                newValue[prop.Metadata.Name] = prop.CurrentValue;
            }

            if (oldValue.Count == 0)
            {
                oldValue = null;
                newValue = null;
            }
        }

        return (oldValue, newValue);
    }
}
