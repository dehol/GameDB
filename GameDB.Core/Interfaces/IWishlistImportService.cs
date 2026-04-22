using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

/// <summary>
/// Service for importing a user's wishlist from an external store (Steam, GOG, EGS).
/// </summary>
public interface IWishlistImportService
{
    /// <summary>
    /// Запустити процес імпорту вішлісту.
    /// Fetches the wishlist from the specified store and adds matching games to the user's local wishlist.
    /// </summary>
    /// <param name="userId">Internal application user ID.</param>
    /// <param name="shopId">Internal shop/platform ID.</param>
    /// <param name="externalUserId">User identifier on the external platform.</param>
    /// <param name="accessToken">Optional OAuth access token (required for EGS).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="ImportResult"/> describing what was imported, skipped, or failed.</returns>
    Task<ImportResult> ImportUserWishlistAsync(
        int userId,
        int shopId,
        string externalUserId,
        string? accessToken = null,
        CancellationToken ct = default);

    /// <summary>
    /// Отримати статус імпорту.
    /// Retrieves the current status of a specific import operation.
    /// </summary>
    /// <param name="importId">The import record ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="WishlistImport"/> record, or <c>null</c> if not found.</returns>
    Task<WishlistImport?> GetImportStatusAsync(int importId, CancellationToken ct = default);

    /// <summary>
    /// Отримати історію імпортів користувача.
    /// Returns the most recent import records for the given user.
    /// </summary>
    /// <param name="userId">Internal application user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="WishlistImport"/> records ordered by most recent first.</returns>
    Task<List<WishlistImport>> GetUserImportHistoryAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Скасувати активний імпорт.
    /// Requests cancellation of an in-progress import.
    /// </summary>
    /// <param name="importId">The import record ID to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the import was found and cancellation was requested; otherwise <c>false</c>.</returns>
    Task<bool> CancelImportAsync(int importId, CancellationToken ct = default);
}

/// <summary>
/// Describes the outcome of a wishlist import operation.
/// </summary>
public class ImportResult
{
    /// <summary>The ID of the <see cref="WishlistImport"/> record created for this operation.</summary>
    public int ImportId { get; set; }

    /// <summary>Number of games successfully added to the wishlist.</summary>
    public int ImportedCount { get; set; }

    /// <summary>Number of games already present in the wishlist and therefore skipped.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Number of games that could not be imported due to errors.</summary>
    public int ErrorCount { get; set; }

    /// <summary>Collection of per-game error messages that occurred during import.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Final status string: "Completed", "Failed", or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Total wall-clock duration of the import operation.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>UTC timestamp when the import finished.</summary>
    public DateTime CompletedAt { get; set; }
}
