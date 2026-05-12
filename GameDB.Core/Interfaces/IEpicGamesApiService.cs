using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

public interface IEpicGamesApiService
{
    Task<List<GameWishlistItem>> GetWishlistAsync(
        string epicUserId,
        string accessToken,
        CancellationToken ct = default);
}
