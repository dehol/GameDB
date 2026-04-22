using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

/// <summary>
/// Service for fetching wishlist and price data from the Epic Games Store platform.
/// </summary>
public interface IEpicGamesApiService
{
    /// <summary>
    /// Retrieves the wishlist items for a given Epic Games user.
    /// </summary>
    /// <param name="epicUserId">Epic Games user identifier.</param>
    /// <param name="accessToken">OAuth access token required by the Epic Games API.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of wishlist items.</returns>
    Task<List<GameWishlistItem>> GetWishlistAsync(string epicUserId, string accessToken, CancellationToken ct = default);
}
