using System.Collections.Concurrent;
using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

/// <summary>
/// Implements wishlist import from Steam, GOG, and Epic Games Store.
/// Manages per-import cancellation tokens and persists progress to the database.
/// </summary>
public class WishlistImportService : IWishlistImportService
{
    private readonly AppDbContext _dbContext;
    private readonly ISteamApiService _steamApi;
    private readonly IGogApiService _gogApi;
    private readonly IEpicGamesApiService _epicApi;
    private readonly IGameMatchingService _gameMatching;
    private readonly ILogger<WishlistImportService> _logger;

    // Tracks CancellationTokenSources for in-progress imports to support cancellation.
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeImports = new();

    public WishlistImportService(
        AppDbContext dbContext,
        ISteamApiService steamApi,
        IGogApiService gogApi,
        IEpicGamesApiService epicApi,
        IGameMatchingService gameMatching,
        ILogger<WishlistImportService> logger)
    {
        _dbContext = dbContext;
        _steamApi = steamApi;
        _gogApi = gogApi;
        _epicApi = epicApi;
        _gameMatching = gameMatching;
        _logger = logger;
    }

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
    public async Task<ImportResult> ImportUserWishlistAsync(
        int userId,
        int shopId,
        string externalUserId,
        string? accessToken = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new ImportResult();

        try
        {
            // 1. Create the import record so progress can be tracked
            var import = new WishlistImport
            {
                UserId = userId,
                ShopId = shopId,
                Status = "InProgress",
                StartedAt = startTime,
                ItemsCount = 0,
                ImportedCount = 0,
                SkippedCount = 0,
                ErrorCount = 0
            };

            _dbContext.WishlistImports.Add(import);
            await _dbContext.SaveChangesAsync(ct);

            result.ImportId = import.ImportId;

            // 2. Create a linked CTS so the caller or CancelImportAsync can abort
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _activeImports.TryAdd(import.ImportId, cts);

            try
            {
                // 3. Fetch wishlist items from the external store
                var wishlistItems = await GetWishlistItemsAsync(shopId, externalUserId, accessToken, cts.Token);

                import.ItemsCount = wishlistItems.Count;
                await _dbContext.SaveChangesAsync(cts.Token);

                _logger.LogInformation(
                    "Starting import of {Count} games for user {UserId} from shop {ShopId}",
                    wishlistItems.Count, userId, shopId);

                // 4. Process each game individually; failures are logged but do not stop the import
                foreach (var item in wishlistItems)
                {
                    try
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        // Match to an existing game or create a new record
                        var (game, _) = await _gameMatching.MatchOrCreateGameAsync(
                            item.ExternalId,
                            shopId,
                            item.Title,
                            item.Description,
                            null,
                            cts.Token);

                        // Skip if already in the user's wishlist
                        var existingWishlist = await _dbContext.Wishlists
                            .FirstOrDefaultAsync(w => w.UserId == userId && w.GameId == game.GameId, cts.Token);

                        if (existingWishlist == null)
                        {
                            var wishlistEntry = new Wishlist
                            {
                                UserId = userId,
                                GameId = game.GameId,
                                SourceShopId = shopId,
                                AddedAt = DateTime.UtcNow
                            };

                            _dbContext.Wishlists.Add(wishlistEntry);
                            await _dbContext.SaveChangesAsync(cts.Token);

                            result.ImportedCount++;
                        }
                        else
                        {
                            result.SkippedCount++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Failed to import '{item.Title}': {ex.Message}");

                        _logger.LogWarning(ex,
                            "Failed to import game '{Title}' for user {UserId}",
                            item.Title, userId);
                    }
                }

                // 5. Mark import as completed
                import.Status = "Completed";
                import.CompletedAt = DateTime.UtcNow;

                result.Status = "Completed";
            }
            catch (OperationCanceledException)
            {
                import.Status = "Cancelled";
                import.CompletedAt = DateTime.UtcNow;
                import.ErrorMessage = "Import was cancelled by user";

                result.Status = "Cancelled";

                _logger.LogInformation("Import {ImportId} was cancelled", import.ImportId);
            }
            catch (Exception ex)
            {
                import.Status = "Failed";
                import.CompletedAt = DateTime.UtcNow;
                import.ErrorMessage = ex.Message;

                result.Status = "Failed";
                result.Errors.Add(ex.Message);

                _logger.LogError(ex, "Import {ImportId} failed", import.ImportId);
            }
            finally
            {
                import.ImportedCount = result.ImportedCount;
                import.SkippedCount = result.SkippedCount;
                import.ErrorCount = result.ErrorCount;

                _dbContext.WishlistImports.Update(import);
                await _dbContext.SaveChangesAsync(ct);

                _activeImports.TryRemove(result.ImportId, out _);
            }

            result.Duration = DateTime.UtcNow - startTime;
            result.CompletedAt = DateTime.UtcNow;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during wishlist import for user {UserId}", userId);
            result.Status = "Failed";
            result.Errors.Add(ex.Message);
            result.Duration = DateTime.UtcNow - startTime;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Отримати статус імпорту.
    /// Retrieves the persisted import record for the given import ID.
    /// </summary>
    /// <param name="importId">The import record ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="WishlistImport"/> record, or <c>null</c> if not found.</returns>
    public async Task<WishlistImport?> GetImportStatusAsync(int importId, CancellationToken ct = default)
    {
        return await _dbContext.WishlistImports
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ImportId == importId, ct);
    }

    /// <summary>
    /// Отримати історію імпортів користувача.
    /// Returns the 50 most recent import records for the given user.
    /// </summary>
    /// <param name="userId">Internal application user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="WishlistImport"/> records ordered by most recent first.</returns>
    public async Task<List<WishlistImport>> GetUserImportHistoryAsync(int userId, CancellationToken ct = default)
    {
        return await _dbContext.WishlistImports
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.StartedAt)
            .Take(50)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Скасувати активний імпорт.
    /// Signals the CancellationTokenSource tied to the specified in-progress import.
    /// </summary>
    /// <param name="importId">The import record ID to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the import was active and cancellation was requested; otherwise <c>false</c>.</returns>
    public Task<bool> CancelImportAsync(int importId, CancellationToken ct = default)
    {
        if (_activeImports.TryGetValue(importId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancellation requested for import {ImportId}", importId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Dispatches to the correct store API based on the shop name stored in the database.
    /// </summary>
    private async Task<List<GameWishlistItem>> GetWishlistItemsAsync(
        int shopId,
        string externalUserId,
        string? accessToken,
        CancellationToken ct)
    {
        var shop = await _dbContext.GameShops
            .FirstOrDefaultAsync(s => s.ShopId == shopId, ct);

        if (shop == null)
            throw new InvalidOperationException($"Shop with ID {shopId} not found");

        _logger.LogInformation(
            "Fetching wishlist from {ShopName} for external user {ExternalUserId}",
            shop.Name, externalUserId);

        return shop.Name.ToLowerInvariant() switch
        {
            "steam" => await _steamApi.GetWishlistAsync(externalUserId, ct),
            "gog" => await _gogApi.GetWishlistAsync(externalUserId, ct),
            "epic games store" or "epic" or "epicgames" =>
                accessToken == null
                    ? throw new ArgumentException("Epic Games Store requires an access token")
                    : await _epicApi.GetWishlistAsync(externalUserId, accessToken, ct),
            _ => throw new InvalidOperationException($"Wishlist import is not supported for shop: {shop.Name}")
        };
    }
}
