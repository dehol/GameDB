using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

public interface IGameMatchingService
{
    Task<(Game game, bool isNew)> MatchOrCreateGameAsync(
        string externalId,
        int shopId,
        string title,
        string? description,
        string? imageUrl,
        CancellationToken ct = default);
}
