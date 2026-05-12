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
                (l.OldValue != null && l.OldValue.Contains(search)) ||
                (l.NewValue != null && l.NewValue.Contains(search)) ||
                (l.IPAddress != null && l.IPAddress.Contains(search)));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.AuditLogId,
                l.UserId,
                username = l.UserId == null
                    ? null
                    : _db.Users.Where(u => u.UserId == l.UserId.Value).Select(u => u.Username).FirstOrDefault(),
                l.Timestamp,
                l.ActionType,
                l.EntityId,
                l.OldValue,
                l.NewValue,
                l.IPAddress
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
