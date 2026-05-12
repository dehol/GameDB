using GameDB.Core.Constants;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

public class GameMatchingService : IGameMatchingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<GameMatchingService> _logger;

    public GameMatchingService(AppDbContext db, ILogger<GameMatchingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(Game game, bool isNew)> MatchOrCreateGameAsync(
        string externalId,
        int shopId,
        string title,
        string? description,
        string? imageUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External ID is required for wishlist imports.", nameof(externalId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required for wishlist imports.", nameof(title));

        var existingOffer = await _db.GameOffers
            .Include(o => o.Game)
            .FirstOrDefaultAsync(o => o.ShopId == shopId && o.ExternalId == externalId, ct);

        if (existingOffer != null)
        {
            return (existingOffer.Game, false);
        }

        var normalizedTitle = NormalizeTitle(title);
        Game? game = null;

        if (!string.IsNullOrEmpty(normalizedTitle))
        {
            game = await _db.Games.FirstOrDefaultAsync(g => g.NormalizedTitle == normalizedTitle, ct);
        }

        if (game == null)
        {
            game = new Game
            {
                Title = title,
                NormalizedTitle = string.IsNullOrEmpty(normalizedTitle) ? null : normalizedTitle,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Games.Add(game);
            await _db.SaveChangesAsync(ct);

            _db.GameOffers.Add(BuildOffer(game.GameId, shopId, externalId));
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Created new game {Title} (GameId={GameId}) from wishlist import", title, game.GameId);
            return (game, true);
        }

        if (!string.IsNullOrEmpty(description) && string.IsNullOrEmpty(game.Description))
        {
            game.Description = description;
            game.UpdatedAt = DateTime.UtcNow;
        }

        var shopOffer = await _db.GameOffers
            .FirstOrDefaultAsync(o => o.GameId == game.GameId && o.ShopId == shopId, ct);

        if (shopOffer == null)
        {
            _db.GameOffers.Add(BuildOffer(game.GameId, shopId, externalId));
            await _db.SaveChangesAsync(ct);
        }
        else if (string.IsNullOrEmpty(shopOffer.ExternalId))
        {
            shopOffer.ExternalId = externalId;
            if (string.IsNullOrEmpty(shopOffer.DownloadUrl))
                shopOffer.DownloadUrl = ShopConstants.GetStoreUrl(shopId, externalId);

            await _db.SaveChangesAsync(ct);
        }
        else if (!string.Equals(shopOffer.ExternalId, externalId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Existing offer for GameId {GameId} shop {ShopId} has ExternalId {ExistingExternalId}, incoming {ExternalId}",
                game.GameId, shopId, shopOffer.ExternalId, externalId);
        }

        return (game, false);
    }

    private static GameOffer BuildOffer(int gameId, int shopId, string externalId)
    {
        return new GameOffer
        {
            GameId = gameId,
            ShopId = shopId,
            ExternalId = externalId,
            CurrentPrice = 0,
            CurrentDiscount = 0,
            Currency = "USD",
            DownloadUrl = ShopConstants.GetStoreUrl(shopId, externalId)
        };
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        return new string(title.ToLower()
            .Replace(":", "").Replace("-", " ").Replace("'", "")
            .Replace("™", "").Replace("®", "").Replace("©", "")
            .Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray())
            .Trim().Replace("  ", " ");
    }
}
