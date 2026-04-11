using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GameDB.Infrastructure.Services;

public class ProfileService
{
    private readonly AppDbContext _db;
    public ProfileService(AppDbContext db) => _db = db;

    public async Task<User?> GetProfileAsync(int userId)
    {
        return await _db.Users
            .Include(u => u.Role)
            .Include(u => u.ShopProfiles).ThenInclude(sp => sp.Shop)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<(bool success, string? error)> UpsertShopProfileAsync(int userId, int shopId, string externalUid)
    {
        if (!await _db.GameShops.AnyAsync(s => s.ShopId == shopId))
            return (false, "Shop not found");

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
        return (true, null);
    }
}
