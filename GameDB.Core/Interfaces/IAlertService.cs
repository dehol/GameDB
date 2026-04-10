namespace GameDB.Core.Interfaces;

public record UserAlertRow
{
    public int alert_id { get; init; }
    public string game_title { get; init; } = null!;
    public decimal? target_price { get; init; }
    public short? target_discount { get; init; }
    public decimal? best_current_price { get; init; }
    public short? best_current_discount { get; init; }
    public decimal? price_gap_pct { get; init; }
    public bool is_active { get; init; }
    public DateTime? triggered_at { get; init; }
    public DateTime created_at { get; init; }
}

public interface IAlertService
{
    Task<List<UserAlertRow>> GetUserAlertsAsync(int userId);
    Task<string?> SetAlertAsync(int userId, int gameId, decimal? targetPrice, short? targetDiscount);
    Task<(bool success, string? error)> UpdateAlertAsync(int userId, int alertId, decimal? targetPrice, short? targetDiscount);
    Task<bool> DeleteAlertAsync(int userId, int alertId);
}
