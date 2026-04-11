using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class SyncController : ControllerBase
{
    private readonly PriceSyncService _sync;
    public SyncController(PriceSyncService sync) => _sync = sync;

    [HttpPost("steam")]
    public async Task<IActionResult> SyncSteam()
    {
        var result = await _sync.SyncSteamPricesAsync();
        return Ok(result);
    }

    [HttpPost("gog")]
    public async Task<IActionResult> SyncGog()
    {
        var result = await _sync.SyncGogPricesAsync();
        return Ok(result);
    }

    [HttpGet("log")]
    public IActionResult GetLog()
    {
        return Ok(new { message = "Sync log is available via PriceHistory table. No dedicated SyncLog table exists." });
    }
}
