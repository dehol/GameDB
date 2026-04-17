using System.Text.Json;
using GameDB.Core.DTOs;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GameDB.Infrastructure.Services;

public class NotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<NotificationItemDto>> GetByUserAsync(int userId)
    {
        await EnsureTriggeredAlertNotificationsAsync(userId);

        return await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationItemDto(
                n.NotificationId,
                n.Type,
                n.Payload,
                n.IsRead,
                n.CreatedAt))
            .ToListAsync();
    }

    public async Task<bool> MarkReadAsync(int userId, int notificationId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);
        if (notification == null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<int> MarkAllReadAsync(int userId)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (unread.Count == 0) return 0;

        foreach (var item in unread)
            item.IsRead = true;

        await _db.SaveChangesAsync();
        return unread.Count;
    }

    private async Task EnsureTriggeredAlertNotificationsAsync(int userId)
    {
        var alertsToNotify = await _db.Alerts
            .Include(a => a.Game)
            .Where(a =>
                a.UserId == userId &&
                a.IsActive == false &&
                a.TriggeredAt != null &&
                a.LastNotifiedAt == null)
            .ToListAsync();

        if (alertsToNotify.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var alert in alertsToNotify)
        {
            var payload = JsonSerializer.Serialize(new
            {
                alertId = alert.AlertId,
                gameId = alert.GameId,
                gameTitle = alert.Game.Title,
                targetPrice = alert.TargetPrice,
                targetDiscount = alert.TargetDiscount,
                triggeredAt = alert.TriggeredAt
            });

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = "price_alert_triggered",
                Payload = payload,
                CreatedAt = now
            });

            alert.LastNotifiedAt = now;
        }

        await _db.SaveChangesAsync();
    }
}
