using GameDB.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/admin/audit-log")]
[Authorize(Roles = "admin")]
public class AuditLogController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditLogController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int? userId = null,
        [FromQuery] string? actionType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(l => l.ActionType.Contains(actionType));
        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l =>
                (l.ActionType != null && l.ActionType.Contains(search)) ||
                (l.EntityId != null && l.EntityId.Contains(search)) ||
                (l.IPAddress != null && l.IPAddress.Contains(search)));
        }

        var total = await query.CountAsync();

        var users = _db.Users.AsNoTracking().Select(u => new { u.UserId, u.Username });

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(
                users,
                log => log.UserId,
                user => (int?)user.UserId,
                (log, userGroup) => new { log, userGroup })
            .SelectMany(
                x => x.userGroup.DefaultIfEmpty(),
                (x, user) => new
                {
                    x.log.AuditLogId,
                    x.log.UserId,
                    username = user != null ? user.Username : null,
                    x.log.Timestamp,
                    x.log.ActionType,
                    x.log.EntityId,
                    x.log.OldValue,
                    x.log.NewValue,
                    x.log.IPAddress
                })
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize),
            items
        });
    }
}
