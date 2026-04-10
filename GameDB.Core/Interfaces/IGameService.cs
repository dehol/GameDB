using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

public record GameCatalogRow
{
    public int GameId { get; init; }
    public string Title { get; init; } = null!;
    public string? Description { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? developer_name { get; init; }
    public string? publisher_name { get; init; }
    public string? genres { get; init; }
    public decimal? min_price { get; init; }
    public short? max_discount { get; init; }
    public long? available_in_shops { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record DealScoreRow
{
    public int listing_id { get; init; }
    public string shop_name { get; init; } = null!;
    public decimal current_price { get; init; }
    public short current_discount { get; init; }
    public decimal historical_low { get; init; }
    public decimal historical_avg { get; init; }
    public short deal_score { get; init; }
    public bool is_historical_low { get; init; }
}

public interface IGameService
{
    Task<(List<GameCatalogRow> items, int totalCount)> GetCatalogAsync(string? search, int? genreId, int? shopId, int page, int pageSize);
    Task<Game?> GetByIdAsync(int gameId);
    Task<List<DealScoreRow>> GetDealScoresAsync(int gameId);
    Task<Game> CreateAsync(Game game, List<int> genreIds);
    Task<Game?> UpdateAsync(int gameId, Game updated, List<int> genreIds);
    Task<bool> DeleteAsync(int gameId);
}
