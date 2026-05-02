using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

/// <summary>
/// Handles shop account linking via direct external ID input.
/// Steam: Steam64 numeric ID. GOG: username.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ShopLinkController : ControllerBase
{
    private readonly ShopLinkService _shopLink;

    public ShopLinkController(ShopLinkService shopLink)
    {
        _shopLink = shopLink;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Links a shop account by external ID directly.
    /// Steam: Steam64 ID (numeric). GOG: username.
    /// </summary>
    [HttpPost("{shop}/link")]
    [Authorize]
    public async Task<IActionResult> LinkByExternalId(string shop, [FromBody] LinkAccountDto dto)
    {
        var shopId = ShopLinkService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}. Supported: steam, gog" });

        var (success, error) = await _shopLink.LinkByExternalIdAsync(GetUserId(), shopId.Value, dto.ExternalId);
        if (!success)
            return BadRequest(new { error });

        return Ok(new { message = $"{shop} account linked successfully", externalId = dto.ExternalId });
    }

    /// <summary>
    /// Unlinks a shop account from the current user.
    /// </summary>
    [HttpDelete("{shop}/unlink")]
    [Authorize]
    public async Task<IActionResult> Unlink(string shop)
    {
        var shopId = ShopLinkService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}" });

        var removed = await _shopLink.UnlinkAsync(GetUserId(), shopId.Value);
        if (!removed)
            return NotFound(new { error = "Shop account not linked" });

        return Ok(new { message = "Shop account unlinked" });
    }

    /// <summary>
    /// Checks if the current user has linked a specific shop account.
    /// </summary>
    [HttpGet("{shop}/status")]
    [Authorize]
    public async Task<IActionResult> Status(string shop)
    {
        var shopId = ShopLinkService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}" });

        var linked = await _shopLink.IsLinkedAsync(GetUserId(), shopId.Value);
        var profile = await _shopLink.GetProfileAsync(GetUserId(), shopId.Value);

        return Ok(new
        {
            linked,
            shopId = shopId.Value,
            shopName = shop,
            externalUid = profile?.ExternalUid
        });
    }

    public record LinkAccountDto(string ExternalId);
}
