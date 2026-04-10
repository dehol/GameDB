using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

public interface IProfileService
{
    Task<User?> GetProfileAsync(int userId);
    Task<(bool success, string? error)> UpsertShopProfileAsync(int userId, int shopId, string externalUid);
}
