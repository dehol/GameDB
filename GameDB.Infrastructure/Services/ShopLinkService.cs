using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

public class ShopLinkService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ShopLinkService> _logger;

    public ShopLinkService(
        AppDbContext db,
        ILogger<ShopLinkService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static int? ShopSlugToId(string slug) => slug.ToLowerInvariant() switch
    {
        "steam" => 1,
        "gog"   => 2,
        _ => null
    };

    public static string? ShopIdToSlug(int shopId) => shopId switch
    {
        1 => "steam",
        2 => "gog",
        _ => null
    };

    /// <summary>
    /// Links a shop account by external ID directly.
    /// Steam: Steam64 numeric ID.
    /// GOG: public GOG username.
    /// </summary>
    public async Task<(bool success, string? error)> LinkByExternalIdAsync(int userId, int shopId, string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return (false, "External ID cannot be empty.");

        externalId = externalId.Trim();

        switch (shopId)
        {
            case 1: // Steam — Steam64 numeric ID
                if (!ulong.TryParse(externalId, out _))
                    return (false, "Steam ID must be a numeric Steam64 ID (e.g. 76561198...).");
                break;

            case 2: // GOG — public username
                if (externalId.Length < 2 || externalId.Length > 64)
                    return (false, "GOG username must be between 2 and 64 characters.");
                break;

            default:
                return (false, $"Unknown shop ID: {shopId}");
        }

        await UpsertProfileAsync(userId, shopId, externalId);
        _logger.LogInformation("User {UserId} linked {Shop} account: {ExternalId}",
            userId, ShopIdToSlug(shopId), externalId);
        return (true, null);
    }

    /// <summary>
    /// Checks if user has linked a shop account (has ExternalUid).
    /// </summary>
    public async Task<bool> IsLinkedAsync(int userId, int shopId)
    {
        return await _db.UserShopProfiles
            .AnyAsync(p => p.UserId == userId && p.ShopId == shopId && p.ExternalUid != null);
    }

    /// <summary>
    /// Gets the UserShopProfile for a specific user and shop.
    /// </summary>
    public async Task<UserShopProfile?> GetProfileAsync(int userId, int shopId)
    {
        return await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == shopId);
    }

    /// <summary>
    /// Removes the UserShopProfile (unlinks shop account).
    /// </summary>
    public async Task<bool> UnlinkAsync(int userId, int shopId)
    {
        var profile = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == shopId);

        if (profile == null)
            return false;

        _db.UserShopProfiles.Remove(profile);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} unlinked shop {ShopId}", userId, shopId);
        return true;
    }

    #region Private helpers

    private async Task UpsertProfileAsync(int userId, int shopId, string externalUid)
    {
        var existing = await _db.UserShopProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ShopId == shopId);

        if (existing != null)
        {
            existing.ExternalUid = externalUid;
            existing.LinkedAt = DateTime.UtcNow;
        }
        else
        {
            _db.UserShopProfiles.Add(new UserShopProfile
            {
                UserId = userId,
                ShopId = shopId,
                ExternalUid = externalUid
            });
        }

        await _db.SaveChangesAsync();
    }

    #endregion
}
