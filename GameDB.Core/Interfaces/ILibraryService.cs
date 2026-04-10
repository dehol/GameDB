namespace GameDB.Core.Interfaces;

public record UserLibraryRow
{
    public int UserId { get; init; }
    public int GameId { get; init; }
    public string game_title { get; init; } = null!;
    public DateOnly? ReleaseDate { get; init; }
    public string? developer_name { get; init; }
    public string shop_name { get; init; } = null!;
    public string? shop_url { get; init; }
    public string? DownloadUrl { get; init; }
    public decimal? purchase_store_price { get; init; }
    public DateTime AddedAt { get; init; }
}

public interface ILibraryService
{
    Task<List<UserLibraryRow>> GetLibraryAsync(int userId);
    Task<string?> AddToLibraryAsync(int userId, int gameId, int shopId);
}
