using GameDB.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly IWishlistService _wishlist;
    public WishlistController(IWishlistService wishlist) => _wishlist = wishlist;

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

    [HttpPost("import-steam")]
    public async Task<IActionResult> ImportSteam()
    {
        var (imported, error) = await _wishlist.ImportSteamAsync(GetUserId());
        if (error != null && imported == 0) return BadRequest(error);
        return Ok(new { imported, message = error });
    }
}
