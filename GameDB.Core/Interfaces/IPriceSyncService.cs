namespace GameDB.Core.Interfaces;

public record SyncError(int GameOfferId, string? ExternalId, string ErrorMessage, int? StatusCode);

public record SyncResult(int Scanned, int Updated, int Errors, List<SyncError>? ErrorDetails = null);

public interface IPriceSyncService
{
    Task<SyncResult> SyncSteamPricesAsync();
    Task<SyncResult> SyncGogPricesAsync();
}
