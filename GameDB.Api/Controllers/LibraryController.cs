using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LibraryController : ControllerBase
{
    private readonly LibraryService _library;
    private readonly ShopOAuthService _oauth;

    public LibraryController(LibraryService library, ShopOAuthService oauth)
    {
        _library = library;
        _oauth = oauth;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record AddToLibraryDto(int GameId, int ShopId);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await _library.GetLibraryAsync(GetUserId());
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddToLibraryDto dto)
    {
        var error = await _library.AddToLibraryAsync(GetUserId(), dto.GameId, dto.ShopId);
        if (error != null) return BadRequest(error);
        return Ok(new { message = "Added to library" });
    }

    /// <summary>
    /// Import library from a specific shop.
    /// Requires the user to have linked their shop account first.
    /// </summary>
    [HttpPost("import/{shop}")]
    [Authorize(Roles = "user,admin")]
    public async Task<IActionResult> Import(string shop)
    {
        var shopId = ShopOAuthService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}. Supported: steam, gog, egs" });

        var userId = GetUserId();

        if (!await _oauth.IsLinkedAsync(userId, shopId.Value))
        {
            return StatusCode(403, new
            {
                error = $"{shop} account not linked. Please link your account first via POST /api/oauth/{shop}/link"
            });
        }

        var (imported, error) = await _library.ImportAsync(userId, shopId.Value);
        if (error != null && imported == 0) return BadRequest(new { error, imported });
        return Ok(new { imported, message = error });
    }
}
