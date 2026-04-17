using GameDB.Core.DTOs;
using GameDB.Core.Models;
using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly GameService _games;
    private readonly ILogger<GamesController> _logger;

    public GamesController(GameService games, ILogger<GamesController> logger)
    {
        _games = games;
        _logger = logger;
    }

    public record CreateGameDto(string Title, string? Description, DateOnly? ReleaseDate, int? DeveloperId, int? PublisherId, List<int> GenreIds);
    public record UpdateGameDto(string Title, string? Description, DateOnly? ReleaseDate, int? DeveloperId, int? PublisherId, List<int> GenreIds);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? genreId,
        [FromQuery] int? shopId,
        [FromQuery] string? sortBy,
        [FromQuery] string? contentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, totalCount) = await _games.GetCatalogAsync(search, genreId, shopId, sortBy, contentType, page, pageSize);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GameDetailsDto>> GetById(int id)
    {
        var game = await _games.GetByIdAsync(id);
        if (game == null) return NotFound();

        _logger.LogInformation("Returning game {GameId} with {GenreCount} genres and {OfferCount} offers",
            id, game.Genres?.Count ?? 0, game.Offers?.Count ?? 0);

        return Ok(game);
    }

    [HttpGet("{id}/deal-score")]
    public async Task<IActionResult> GetDealScore(int id)
    {
        var scores = await _games.GetDealScoresAsync(id);
        return Ok(scores);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create(CreateGameDto dto)
    {
        var game = new Game
        {
            Title = dto.Title,
            Description = dto.Description,
            ReleaseDate = dto.ReleaseDate,
            DeveloperId = dto.DeveloperId,
            PublisherId = dto.PublisherId
        };

        var created = await _games.CreateAsync(game, dto.GenreIds);
        return CreatedAtAction(nameof(GetById), new { id = created.GameId }, new { created.GameId, created.Title });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, UpdateGameDto dto)
    {
        var updated = new Game
        {
            Title = dto.Title,
            Description = dto.Description,
            ReleaseDate = dto.ReleaseDate,
            DeveloperId = dto.DeveloperId,
            PublisherId = dto.PublisherId
        };

        var result = await _games.UpdateAsync(id, updated, dto.GenreIds);
        if (result == null) return NotFound();
        return Ok(new { result.GameId, result.Title });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _games.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
