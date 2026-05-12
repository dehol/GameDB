using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

public class SteamApiService : ISteamApiService
{
    private readonly ILogger<SteamApiService> _logger;

    public SteamApiService(ILogger<SteamApiService> logger)
    {
        _logger = logger;
    }

    public Task<List<GameWishlistItem>> GetWishlistAsync(string steamUserId, CancellationToken ct = default)
    {
        const string message = "Steam wishlist import is not implemented yet.";
        _logger.LogWarning("{Message} SteamUserId={SteamUserId}", message, steamUserId);
        throw new NotSupportedException(message);
    }
}
