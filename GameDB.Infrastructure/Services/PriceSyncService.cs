using GameDB.Core.DTOs;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

public class PriceSyncService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PriceSyncService> _logger;
    private const int BatchSize = 20; // Steam batch size

    public PriceSyncService(AppDbContext db, IHttpClientFactory httpFactory, ILogger<PriceSyncService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<SyncResult> SyncSteamPricesAsync()
    {
        var offers = await _db.GameOffers
            .Where(o => o.ShopId == 1 && o.ExternalId != null)
            .ToListAsync();

        int updated = 0;
        var errors = new List<SyncError>();
        var client = _httpFactory.CreateClient();

        // Batch processing instead of per-offer calls
        foreach (var batch in offers.Chunk(BatchSize))
        {
            try
            {
                var appIds = string.Join(",", batch.Select(o => o.ExternalId));
                var url = $"https://store.steampowered.com/api/appdetails?appids={appIds}&cc=us&filters=price_overview";
                
                var response = await client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Steam API returned {StatusCode} for batch", (int)response.StatusCode);
                    errors.AddRange(batch.Select(o => 
                        new SyncError(o.GameOfferId, o.ExternalId, $"HTTP {(int)response.StatusCode}", (int)response.StatusCode)));
                    await Task.Delay(200);
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                using var doc = JsonDocument.Parse(content); // Fixed: added using

                foreach (var offer in batch)
                {
                    try
                    {
                        if (!doc.RootElement.TryGetProperty(offer.ExternalId!, out var appData))
                        {
                            errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, "Missing app data", null));
                            continue;
                        }

                        if (!appData.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
                        {
                            errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, "success=false", null));
                            continue;
                        }

                        if (appData.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("price_overview", out var priceObj))
                        {
                            var finalPrice = priceObj.GetProperty("final").GetInt32() / 100m;
                            var discount = (short)priceObj.GetProperty("discount_percent").GetInt32();

                            if (offer.CurrentPrice != finalPrice || offer.CurrentDiscount != discount)
                            {
                                offer.CurrentPrice = finalPrice;
                                offer.CurrentDiscount = discount;
                                offer.PriceSyncedAt = DateTime.UtcNow;
                                updated++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing Steam price for {ExternalId}", offer.ExternalId);
                        errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, ex.Message, null));
                    }
                }

                // Reduced logging overhead: log every 50 updates
                if (updated > 0 && updated % 50 == 0)
                {
                    _logger.LogInformation("Steam sync progress: {Updated} prices updated", updated);
                }

                // Clear ChangeTracker to reduce memory growth
                _db.ChangeTracker.Clear();

                await Task.Delay(200); // Reduced from 500ms
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Steam batch");
                errors.AddRange(batch.Select(o => 
                    new SyncError(o.GameOfferId, o.ExternalId, ex.Message, null)));
            }
        }

        await _db.SaveChangesAsync();
        
        if (updated > 0)
        {
            _logger.LogInformation("Steam sync completed: {Updated}/{Total} updated, {Errors} errors", 
                updated, offers.Count, errors.Count);
        }
        
        return new SyncResult(offers.Count, updated, errors.Count, errors);
    }

    public async Task<SyncResult> SyncGogPricesAsync()
    {
        var offers = await _db.GameOffers
            .Where(o => o.ShopId == 2 && o.ExternalId != null)
            .ToListAsync();

        int updated = 0;
        var errors = new List<SyncError>();
        var client = _httpFactory.CreateClient();

        foreach (var offer in offers)
        {
            try
            {
                var url = $"https://api.gog.com/products/{offer.ExternalId}?expand=prices&countryCode=US";
                var response = await client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GOG API returned {StatusCode} for offer {OfferId}", 
                        (int)response.StatusCode, offer.GameOfferId);
                    errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, 
                        $"HTTP {(int)response.StatusCode}", (int)response.StatusCode));
                    await Task.Delay(200); // Reduced from 500ms
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                using var doc = JsonDocument.Parse(content); // Fixed: added using

                if (doc.RootElement.TryGetProperty("price", out var priceObj))
                {
                    var finalAmount = decimal.Parse(priceObj.GetProperty("finalAmount").GetString()!);
                    var baseAmount = decimal.Parse(priceObj.GetProperty("baseAmount").GetString()!);
                    var discount = baseAmount > 0
                        ? (short)Math.Round((1 - finalAmount / baseAmount) * 100)
                        : (short)0;

                    if (offer.CurrentPrice != finalAmount || offer.CurrentDiscount != discount)
                    {
                        offer.CurrentPrice = finalAmount;
                        offer.CurrentDiscount = discount;
                        offer.PriceSyncedAt = DateTime.UtcNow;
                        updated++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing GOG price for offer {OfferId}", offer.GameOfferId);
                errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, ex.Message, null));
            }

            // Clear ChangeTracker periodically to reduce memory growth
            if (updated % 50 == 0 && updated > 0)
            {
                _db.ChangeTracker.Clear();
                _logger.LogInformation("GOG sync progress: {Updated} prices updated", updated);
            }

            await Task.Delay(200); // Reduced from 500ms
        }

        await _db.SaveChangesAsync();
        
        if (updated > 0)
        {
            _logger.LogInformation("GOG sync completed: {Updated}/{Total} updated, {Errors} errors", 
                updated, offers.Count, errors.Count);
        }
        
        return new SyncResult(offers.Count, updated, errors.Count, errors);
    }
}
