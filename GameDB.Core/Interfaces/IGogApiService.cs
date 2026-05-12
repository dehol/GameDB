using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

public interface IGogApiService
{
    Task<List<GameWishlistItem>> GetWishlistAsync(
        string gogUserId,
        CancellationToken ct = default);
}
