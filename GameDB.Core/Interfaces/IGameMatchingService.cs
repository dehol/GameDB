using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

/// <summary>
/// Service responsible for matching external game identifiers to local Game records,
/// creating new records when no match is found.
/// </summary>
public interface IGameMatchingService
{
    /// <summary>
    /// Finds an existing Game by external store ID, or creates a new one if no match is found.
    /// </summary>
    /// <param name="externalId">The game's identifier on the external store.</param>
    /// <param name="shopId">The shop (platform) identifier.</param>
    /// <param name="title">Game title from the external store.</param>
    /// <param name="description">Game description from the external store.</param>
    /// <param name="imageUrl">Optional cover image URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple with the matched or newly created Game, and a flag indicating whether it was newly created.
    /// </returns>
    Task<(Game game, bool isNew)> MatchOrCreateGameAsync(
        string externalId,
        int shopId,
        string title,
        string? description,
        string? imageUrl,
        CancellationToken ct = default);
}
