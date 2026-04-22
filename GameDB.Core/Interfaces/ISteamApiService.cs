using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

/// <summary>
/// Service for fetching wishlist and price data from the Steam platform.
/// </summary>
public interface ISteamApiService
{
    /// <summary>
    /// Retrieves the wishlist items for a given Steam user.
    /// </summary>
    /// <param name="steamUserId">Steam 64-bit user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of wishlist items.</returns>
    Task<List<GameWishlistItem>> GetWishlistAsync(string steamUserId, CancellationToken ct = default);
}
