using GameDB.Core.Interfaces;
using GameDB.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPipelineService _pipelineService;

    public AdminController(AppDbContext db, IPipelineService pipelineService)
    {
        _db = db;
        _pipelineService = pipelineService;
    }

    [HttpGet("games-management")]
    public async Task<IActionResult> GetGamesManagement(
        [FromQuery] string? search,
        [FromQuery] int? offersCount,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Games.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(g => EF.Functions.ILike(g.Title, $"%{search}%"));
        }

        var projectedQuery = query.Select(g => new
        {
            g.GameId,
            g.Title,
            g.ReleaseDate,
            OffersCount = g.Offers.Count()
        });

        if (offersCount.HasValue)
        {
            projectedQuery = projectedQuery.Where(g => g.OffersCount == offersCount.Value);
        }

        var totalCount = await projectedQuery.CountAsync();
        var items = await projectedQuery
            .OrderBy(g => g.GameId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            items,
            totalCount,
            page,
            pageSize
        });
    }

    public record SyncOffersRequest(List<int>? GameIds);

    [HttpPost("games/sync-offers")]
    public async Task<IActionResult> SyncOffers([FromBody] SyncOffersRequest request)
    {
        var gameIds = request.GameIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (gameIds == null || gameIds.Count == 0)
        {
            return BadRequest(new { error = "At least one valid gameId is required" });
        }

        try
        {
            var pipelineId = await _pipelineService.StartImportPipelineAsync(
                new ImportPipelineOptions(GameIds: gameIds));

            return Ok(new
            {
                pipelineId,
                message = $"Offers sync pipeline started for {gameIds.Count} games",
                statusUrl = $"/api/import/status/{pipelineId}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
