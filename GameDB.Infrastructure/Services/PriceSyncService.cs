using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

public class PriceSyncService : IPriceSyncService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PriceSyncService> _logger;

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

        foreach (var offer in offers)
        {
            try
            {
                var url = $"https://store.steampowered.com/api/appdetails?appids={offer.ExternalId}&cc=us&filters=price_overview";
                var response = await client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Steam API returned {StatusCode} for offer {OfferId} (ExternalId: {ExternalId})", 
                        (int)response.StatusCode, offer.GameOfferId, offer.ExternalId);
                    errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", (int)response.StatusCode));
                    await Task.Delay(500);
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);

                if (!doc.RootElement.TryGetProperty(offer.ExternalId!, out var appData))
                {
                    _logger.LogWarning("Steam API response missing app data for {ExternalId}", offer.ExternalId);
                    errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, "Missing app data in response", null));
                    continue;
                }

                if (!appData.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
                {
                    _logger.LogWarning("Steam API returned success=false for {ExternalId}", offer.ExternalId);
                    errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, "Steam API returned success=false", null));
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
                        _logger.LogInformation("Updated price for game {GameId} (Steam): ${Price} (-{Discount}%)", 
                            offer.GameId, finalPrice, discount);
                    }
                }
                else
                {
                    // Game might be free or no longer available
                    _logger.LogDebug("No price info for Steam app {ExternalId}", offer.ExternalId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Steam price for offer {OfferId} (ExternalId: {ExternalId})", 
                    offer.GameOfferId, offer.ExternalId);
                errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, ex.Message, null));
            }

            await Task.Delay(500); // Increased delay for rate limiting
        }

        await _db.SaveChangesAsync();
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
                    _logger.LogWarning("GOG API returned {StatusCode} for offer {OfferId} (ExternalId: {ExternalId})", 
                        (int)response.StatusCode, offer.GameOfferId, offer.ExternalId);
                    errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", (int)response.StatusCode));
                    await Task.Delay(500);
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);

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
                        _logger.LogInformation("Updated price for game {GameId} (GOG): ${Price} (-{Discount}%)", 
                            offer.GameId, finalAmount, discount);
                    }
                }
                else
                {
                    _logger.LogDebug("No price info for GOG product {ExternalId}", offer.ExternalId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing GOG price for offer {OfferId} (ExternalId: {ExternalId})", 
                    offer.GameOfferId, offer.ExternalId);
                errors.Add(new SyncError(offer.GameOfferId, offer.ExternalId, ex.Message, null));
            }

            await Task.Delay(500);
        }

        await _db.SaveChangesAsync();
        return new SyncResult(offers.Count, updated, errors.Count, errors);
    }
}
