using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

/// <summary>
/// Service for fetching wishlist and price data from the GOG platform.
/// </summary>
public interface IGogApiService
{
    /// <summary>
    /// Retrieves the wishlist items for a given GOG user.
    /// </summary>
    /// <param name="gogUserId">GOG user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of wishlist items.</returns>
    Task<List<GameWishlistItem>> GetWishlistAsync(string gogUserId, CancellationToken ct = default);
}
