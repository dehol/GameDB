using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameDB.Infrastructure.Services;

public class AlertService : IAlertService
{
    private readonly AppDbContext _db;
    public AlertService(AppDbContext db) => _db = db;

    public async Task<List<UserAlertRow>> GetUserAlertsAsync(int userId)
    {
        return await _db.Database
            .SqlQueryRaw<UserAlertRow>("SELECT * FROM fn_get_user_alerts({0})", userId)
            .ToListAsync();
    }

    public async Task<string?> SetAlertAsync(int userId, int gameId, decimal? targetPrice, short? targetDiscount)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "CALL pr_set_alert({0}, {1}, {2}, {3})",
                userId, gameId,
                targetPrice.HasValue ? (object)targetPrice.Value : DBNull.Value,
                targetDiscount.HasValue ? (object)targetDiscount.Value : DBNull.Value);
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.MessageText;
        }
    }

    public async Task<(bool success, string? error)> UpdateAlertAsync(int userId, int alertId, decimal? targetPrice, short? targetDiscount)
    {
        if (targetPrice == null && targetDiscount == null)
            return (false, "Must specify target_price or target_discount");

        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.AlertId == alertId);
        if (alert == null) return (false, "Alert not found");
        if (alert.UserId != userId) return (false, "Access denied");

        alert.TargetPrice = targetPrice;
        alert.TargetDiscount = targetDiscount;
        alert.IsActive = true;
        alert.TriggeredAt = null;
        alert.CreatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> DeleteAlertAsync(int userId, int alertId)
    {
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.AlertId == alertId && a.UserId == userId);
        if (alert == null) return false;
        _db.Alerts.Remove(alert);
        await _db.SaveChangesAsync();
        return true;
    }
}
