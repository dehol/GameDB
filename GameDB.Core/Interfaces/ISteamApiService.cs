using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

public interface ISteamApiService
{
    Task<List<GameWishlistItem>> GetWishlistAsync(
        string steamUserId,
        CancellationToken ct = default);
}
