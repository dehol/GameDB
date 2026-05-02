using EFCore.BulkExtensions;
using GameDB.Core.Configuration;
using GameDB.Core.Constants;
using GameDB.Core.DTOs;
using GameDB.Core.Models;
using GameDB.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameDB.Infrastructure.Services;

public class PriceSyncService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IItadClient _itad;
    private readonly ItadUuidCache _uuidCache;
    private readonly ILogger<PriceSyncService> _logger;
    private readonly ImportSettings _settings;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private const int ItadShopGog = 35;

    public PriceSyncService(
        IServiceProvider serviceProvider,
        IItadClient itad,
        ItadUuidCache uuidCache,
        ILogger<PriceSyncService> logger,
        ImportSettings settings)
    {
        _serviceProvider = serviceProvider;
        _itad = itad;
        _uuidCache = uuidCache;
        _logger = logger;
        _settings = settings;
    }

    public Task<SyncResult> SyncSteamPricesAsync(CancellationToken ct = default)
        => SyncSteamAsync(ct);

    public Task<SyncResult> SyncGogPricesAsync(CancellationToken ct = default)
        => SyncGogAsync(ct);

    public async Task<SyncResult> SyncAllAsync(CancellationToken ct = default)
    {
        if (!_syncLock.Wait(0))
        {
            _logger.LogWarning("Price sync already in progress — skipping");
            return new SyncResult(0, 0, 0, [new SyncError(0, null, "Sync already in progress", null)]);
        }

        try
        {
            var steamTask = SyncSteamAsync(ct);
            var gogTask   = SyncGogAsync(ct);

            await Task.WhenAll(steamTask, gogTask);

            var steamResult = await steamTask;
            var gogResult   = await gogTask;

            var allErrors = new List<SyncError>();
            if (steamResult.ErrorDetails != null) allErrors.AddRange(steamResult.ErrorDetails);
            if (gogResult.ErrorDetails != null) allErrors.AddRange(gogResult.ErrorDetails);

            return new SyncResult(
                steamResult.Scanned + gogResult.Scanned,
                steamResult.Updated + gogResult.Updated,
                steamResult.Errors + gogResult.Errors,
                allErrors
            );
        }
        finally
        {
            _syncLock.Release();
        }
    }

    // ── Steam: direct Steam Store API (ITAD has very limited Steam coverage) ──

    private async Task<SyncResult> SyncSteamAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting Steam price sync via Steam API");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Steam");

        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(_settings.PriceSyncIntervalHours);
        var query = db.GameOffers
            .AsNoTracking()
            .Where(o => o.ShopId == ShopConstants.Steam && o.ExternalId != null
                && (o.PriceSyncedAt == null || o.PriceSyncedAt < cutoff));

        if (_settings.PriceSyncMaxOffers > 0)
            query = query.Take(_settings.PriceSyncMaxOffers);

        var offers = await query.ToListAsync(ct);

        if (offers.Count == 0)
        {
            _logger.LogInformation("Steam: no stale offers to sync");
            return new SyncResult(0, 0, 0, []);
        }

        _logger.LogInformation("Steam: {Count} stale offers to sync", offers.Count);

        var priceMap = new Dictionary<string, (decimal? price, short? discount, bool isFree)>(StringComparer.OrdinalIgnoreCase);
        var batchSize = _settings.SteamBatchSize;

        // Batch Steam app IDs with delay to avoid rate limiting
        var chunks = offers.Select(o => o.ExternalId!).Distinct().Chunk(batchSize).ToList();
        var consecutiveErrors = 0;
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            try
            {
                var ids = string.Join(",", chunk);
                var url = $"https://store.steampowered.com/api/appdetails?appids={ids}&cc=us&filters=basic,price_overview";
                using var resp = await http.GetAsync(url, ct);

                if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    || resp.StatusCode == System.Net.HttpStatusCode.Forbidden
                    || resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    consecutiveErrors++;
                    var waitSeconds = Math.Min(30 * consecutiveErrors, 120);
                    _logger.LogWarning("Steam API {Status} at batch {Batch}/{Total} (x{Count}), waiting {Wait}s",
                        (int)resp.StatusCode, i + 1, chunks.Count, consecutiveErrors, waitSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);

                    // Retry once after cooldown
                    using var retryResp = await http.GetAsync(url, ct);
                    if (retryResp.IsSuccessStatusCode)
                    {
                        var json = await retryResp.Content.ReadAsStringAsync(ct);
                        ParseSteamPrices(json, priceMap);
                        consecutiveErrors = 0;
                    }
                }
                else if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    ParseSteamPrices(json, priceMap);
                    consecutiveErrors = 0;
                }

                // Delay between batches — shorter when no errors, longer after rate limiting
                var delay = consecutiveErrors > 0 ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(1);
                if (i < chunks.Count - 1)
                    await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Steam price batch {Batch} failed", i + 1);
            }
        }

        _logger.LogInformation("Steam: got prices for {Count} apps", priceMap.Count);

        var offersToUpdate = new List<GameOffer>();
        var errors = new List<SyncError>();
        var now = DateTime.UtcNow;
        int noPriceData = 0, unchanged = 0, markedFree = 0;

        foreach (var offer in offers)
        {
            var extId = offer.ExternalId!;

            if (!priceMap.TryGetValue(extId, out var p))
            {
                // Not found in Steam response at all — skip
                noPriceData++;
                continue;
            }

            // Free game detected via is_free flag
            if (p.isFree)
            {
                if (!offer.IsFree || offer.CurrentPrice != 0)
                {
                    offer.IsFree = true;
                    offer.CurrentPrice = 0;
                    offer.CurrentDiscount = 0;
                    offer.PriceSyncedAt = now;
                    offersToUpdate.Add(offer);
                    markedFree++;
                }
                else if (offer.PriceSyncedAt == null)
                {
                    offer.PriceSyncedAt = now;
                    offersToUpdate.Add(offer);
                    unchanged++;
                }
                continue;
            }

            if (p.price == null)
            {
                // No price_overview and not free — unreleased, removed, etc.
                noPriceData++;
                continue;
            }

            if (offer.CurrentPrice == p.price && offer.CurrentDiscount == p.discount)
            {
                // Price unchanged — still mark as synced
                offer.PriceSyncedAt = now;
                offersToUpdate.Add(offer);
                unchanged++;
                continue;
            }

            offer.CurrentPrice    = p.price.Value;
            offer.CurrentDiscount = p.discount ?? 0;
            offer.Currency        = "USD";
            offer.PriceSyncedAt   = now;
            offer.IsFree          = false;

            offersToUpdate.Add(offer);
        }

        _logger.LogInformation(
            "Steam stats: {NoPrice} no price data, {Unchanged} unchanged, {MarkedFree} free, {Updated} price changed",
            noPriceData, unchanged, markedFree, offersToUpdate.Count - unchanged - markedFree);

        if (offersToUpdate.Count > 0)
        {
            await db.BulkUpdateAsync(offersToUpdate, cancellationToken: ct);
        }

        _logger.LogInformation("Steam sync done: {Updated}/{Total} updated, {NoPrice} no price data, {Free} free",
            offersToUpdate.Count, offers.Count, noPriceData, markedFree);
        return new SyncResult(offers.Count, offersToUpdate.Count, errors.Count, errors);
    }

    private static void ParseSteamPrices(string json,
        Dictionary<string, (decimal? price, short? discount, bool isFree)> map)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var data = prop.Value;
            if (data.ValueKind != JsonValueKind.Object) continue;
            if (!data.TryGetProperty("success", out var s) || !s.GetBoolean()) continue;
            if (!data.TryGetProperty("data", out var gd) || gd.ValueKind != JsonValueKind.Object) continue;

            // Check is_free flag
            var isFree = gd.TryGetProperty("is_free", out var ifVal) && ifVal.ValueKind == JsonValueKind.True;

            if (!gd.TryGetProperty("price_overview", out var po) || po.ValueKind != JsonValueKind.Object)
            {
                // No price_overview — if is_free, record as free; otherwise skip
                if (isFree)
                    map[prop.Name] = (0m, (short)0, true);
                continue;
            }

            var final   = po.TryGetProperty("final",   out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0;
            var initial = po.TryGetProperty("initial", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt32() : 0;

            map[prop.Name] = (
                final / 100m,
                initial > 0 ? (short)Math.Round((1d - (double)final / initial) * 100) : (short)0,
                isFree
            );
        }
    }

    // ── GOG: via ITAD API (title lookup → prices) ──

    private async Task<SyncResult> SyncGogAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting GOG price sync via ITAD");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(_settings.PriceSyncIntervalHours);
        var query = db.GameOffers
            .AsNoTracking()
            .Include(o => o.Game)
            .Where(o => o.ShopId == ShopConstants.Gog && o.ExternalId != null
                && (o.PriceSyncedAt == null || o.PriceSyncedAt < cutoff));

        if (_settings.PriceSyncMaxOffers > 0)
            query = query.Take(_settings.PriceSyncMaxOffers);

        var offers = await query.ToListAsync(ct);

        if (offers.Count == 0)
        {
            _logger.LogInformation("GOG: no stale offers to sync");
            return new SyncResult(0, 0, 0, []);
        }

        _logger.LogInformation("GOG: {Count} stale offers to sync", offers.Count);

        var errors = new List<SyncError>();

        // 1. Resolve ITAD UUIDs by title (with caching)
        var titles = offers.ToDictionary(o => o.ExternalId!, o => o.Game?.Title ?? "");
        var distinctTitles = titles.Values.Distinct().ToList();
        var uncachedTitles = distinctTitles.Where(t => !_uuidCache.TryGet($"title:{t}", out _)).ToList();

        _logger.LogInformation("GOG: {Total} distinct titles, {Uncached} need lookup",
            distinctTitles.Count, uncachedTitles.Count);

        if (uncachedTitles.Count > 0)
        {
            var freshMap = await _itad.LookupUuidsByTitleAsync(uncachedTitles, ct);
            _logger.LogInformation("GOG: ITAD title lookup returned {Count} UUIDs", freshMap.Count);
            foreach (var (title, uuid) in freshMap)
                _uuidCache.Set($"title:{title}", uuid);
        }

        // Map: ExternalId -> ITAD UUID
        var itadIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slug, title) in titles)
        {
            if (_uuidCache.TryGet($"title:{title}", out var uuid) && !string.IsNullOrEmpty(uuid))
                itadIdMap[slug] = uuid;
        }

        var allItadUuids = itadIdMap.Values.Where(v => !string.IsNullOrEmpty(v)).Distinct().ToList();

        if (allItadUuids.Count == 0)
        {
            _logger.LogWarning("GOG: no ITAD UUIDs resolved — skipping price fetch.");
            return new SyncResult(offers.Count, 0, offers.Count, []);
        }

        _logger.LogInformation("GOG: {Count} ITAD UUIDs resolved, fetching prices", allItadUuids.Count);

        // 2. Fetch prices
        var priceMap = await _itad.GetPricesAsync(ItadShopGog, allItadUuids, ct);

        _logger.LogInformation("GOG: ITAD returned prices for {Count} games", priceMap.Count);

        // 3. Update DB
        var offersToUpdate = new List<GameOffer>();
        var now = DateTime.UtcNow;
        int noUuid = 0, noPrice = 0, unchanged = 0, markedFree = 0;

        foreach (var offer in offers)
        {
            var extId = offer.ExternalId!;

            if (!itadIdMap.TryGetValue(extId, out var itadUuid) || string.IsNullOrEmpty(itadUuid))
            {
                noUuid++;
                continue;
            }

            if (!priceMap.TryGetValue(itadUuid, out var price))
            {
                noPrice++;
                continue;
            }

            // ITAD returns price=0 for free games
            if (price.Amount == 0)
            {
                if (!offer.IsFree)
                {
                    offer.IsFree = true;
                    offer.CurrentPrice = 0;
                    offer.CurrentDiscount = 0;
                    offer.PriceSyncedAt = now;
                    offersToUpdate.Add(offer);
                    markedFree++;
                }
                else if (offer.PriceSyncedAt == null)
                {
                    offer.PriceSyncedAt = now;
                    offersToUpdate.Add(offer);
                    unchanged++;
                }
                continue;
            }

            if (offer.CurrentPrice == price.Amount && offer.CurrentDiscount == price.Cut)
            {
                unchanged++;
                // Still mark as synced even if price unchanged
                offer.PriceSyncedAt = now;
                offersToUpdate.Add(offer);
                continue;
            }

            offer.CurrentPrice    = price.Amount;
            offer.CurrentDiscount = (short)price.Cut;
            offer.Currency        = price.Currency;
            offer.PriceSyncedAt   = now;
            offer.IsFree          = false;

            offersToUpdate.Add(offer);
        }

        _logger.LogInformation(
            "GOG stats: {NoUuid} no UUID, {NoPrice} no price, {Unchanged} unchanged, {Free} free, {Updated} price changed",
            noUuid, noPrice, unchanged, markedFree, offersToUpdate.Count - unchanged - markedFree);

        if (offersToUpdate.Count > 0)
        {
            await db.BulkUpdateAsync(offersToUpdate, cancellationToken: ct);
        }

        _logger.LogInformation("GOG sync done: {Updated}/{Total} updated", offersToUpdate.Count, offers.Count);
        return new SyncResult(offers.Count, offersToUpdate.Count, errors.Count, errors);
    }
}

public class ItadSettings
{
    public string ApiKey { get; set; } = string.Empty;
}
