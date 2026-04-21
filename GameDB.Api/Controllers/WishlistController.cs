using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly WishlistService _wishlist;
    private readonly ShopOAuthService _oauth;

    public WishlistController(WishlistService wishlist, ShopOAuthService oauth)
    {
        _wishlist = wishlist;
        _oauth = oauth;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await _wishlist.GetByUserAsync(GetUserId());
        return Ok(items);
    }

    [HttpPost("{gameId}")]
    public async Task<IActionResult> Add(int gameId)
    {
        var (success, error) = await _wishlist.AddAsync(GetUserId(), gameId);
        if (!success) return BadRequest(error);
        return Ok(new { message = "Added to wishlist" });
    }

    [HttpDelete("{gameId}")]
    public async Task<IActionResult> Remove(int gameId)
    {
        var removed = await _wishlist.RemoveAsync(GetUserId(), gameId);
        if (!removed) return NotFound();
        return NoContent();
    }

    [HttpPost("{gameId}/toggle")]
    public async Task<IActionResult> Toggle(int gameId)
    {
        var (added, message) = await _wishlist.ToggleAsync(GetUserId(), gameId);
        return Ok(new { added, message });
    }

    /// <summary>
    /// Import wishlist from a specific shop.
    /// Requires the user to have linked their shop account via OAuth first.
    /// </summary>
    [HttpPost("import/{shop}")]
    [Authorize(Roles = "user,admin")]
    public async Task<IActionResult> Import(string shop)
    {
        var shopId = ShopOAuthService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}. Supported: steam, gog, egs" });

        var userId = GetUserId();

        // Check if user has linked the shop account
        if (!await _oauth.HasValidAuthAsync(userId, shopId.Value))
        {
            var (authUrl, _) = _oauth.GetAuthorizationUrl(userId, shopId.Value);
            return StatusCode(403, new
            {
                error = $"{shop} account not linked. Please link your account first.",
                authorizeUrl = authUrl
            });
        }

        var (imported, error) = await _wishlist.ImportAsync(userId, shopId.Value);
        if (error != null && imported == 0) return BadRequest(new { error, imported });
        return Ok(new { imported, message = error });
    }
}
