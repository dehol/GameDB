namespace GameDB.Core.Interfaces;

public record WishlistItemDto(int GameId, string GameTitle, DateTime AddedAt, string? SourceShop);

public interface IWishlistService
{
    Task<List<WishlistItemDto>> GetByUserAsync(int userId);
    Task<(bool success, string? error)> AddAsync(int userId, int gameId);
    Task<bool> RemoveAsync(int userId, int gameId);
    Task<(bool added, string message)> ToggleAsync(int userId, int gameId);
    Task<(int imported, string? error)> ImportSteamAsync(int userId);
}
