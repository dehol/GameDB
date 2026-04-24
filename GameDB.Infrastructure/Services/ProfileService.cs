using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

public class ProfileService
{
    private readonly AppDbContext _db;
    private readonly ShopOAuthService _oauth;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(AppDbContext db, ShopOAuthService oauth, ILogger<ProfileService> logger)
    {
        _db = db;
        _oauth = oauth;
        _logger = logger;
    }

    public async Task<User?> GetProfileAsync(int userId)
    {
        return await _db.Users
            .Include(u => u.Role)
            .Include(u => u.ShopProfiles).ThenInclude(sp => sp.Shop)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    /// <summary>
    /// Links a shop account by external ID.
    /// Validates the ID format via ShopOAuthService.LinkByExternalIdAsync.
    /// </summary>
    public async Task<(bool success, string? error)> UpsertShopProfileAsync(int userId, int shopId, string externalUid)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            return (false, "User not found");

        if (user.IsGuest)
            return (false, "Guest accounts cannot link shop profiles");

        if (string.IsNullOrWhiteSpace(externalUid))
            return (false, "External UID is required");

        // Delegate validation + upsert to ShopOAuthService
        return await _oauth.LinkByExternalIdAsync(userId, shopId, externalUid);
    }

    /// <summary>
    /// Unlinks a shop account from user profile.
    /// </summary>
    public async Task<(bool success, string? error)> UnlinkShopProfileAsync(int userId, int shopId)
    {
        var removed = await _oauth.UnlinkAsync(userId, shopId);
        return (removed, removed ? null : "Shop account not linked");
    }
}
