using GameDB.Core.Interfaces;
using GameDB.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameDB.Infrastructure.Services;

public class EpicGamesApiService : IEpicGamesApiService
{
    private readonly ILogger<EpicGamesApiService> _logger;

    public EpicGamesApiService(ILogger<EpicGamesApiService> logger)
    {
        _logger = logger;
    }

    public Task<List<GameWishlistItem>> GetWishlistAsync(string epicUserId, string accessToken, CancellationToken ct = default)
    {
        const string message = "Epic Games Store wishlist import is not implemented yet.";
        _logger.LogWarning("{Message} EpicUserId={EpicUserId}", message, epicUserId);
        throw new NotSupportedException(message);
    }
}
