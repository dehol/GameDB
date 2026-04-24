using GameDB.Core.Configuration;
using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

/// <summary>
/// Handles shop account linking.
/// Steam: supports OpenID login for automatic Steam64 ID retrieval.
/// GOG/EGS: direct external ID input (username/display name) — no OAuth needed.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OAuthController : ControllerBase
{
    private readonly ShopOAuthService _oauth;
    private readonly OAuthSettings _oauthSettings;

    public OAuthController(ShopOAuthService oauth, OAuthSettings oauthSettings)
    {
        _oauth = oauth;
        _oauthSettings = oauthSettings;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Initiates Steam OpenID login. Returns the authorization URL.
    /// </summary>
    [HttpGet("steam/authorize")]
    [Authorize]
    public IActionResult SteamAuthorize()
    {
        var (url, state) = _oauth.GetSteamAuthUrl(GetUserId());
        return Ok(new { url, state });
    }

    /// <summary>
    /// Handles Steam OpenID callback.
    /// </summary>
    [HttpGet("steam/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> SteamCallback(
        [FromQuery(Name = "state")] string? state,
        [FromQuery(Name = "openid.claimed_id")] string? claimedId)
    {
        var frontendUrl = _oauthSettings.FrontendBaseUrl;

        if (string.IsNullOrWhiteSpace(claimedId) || string.IsNullOrWhiteSpace(state))
            return Redirect($"{frontendUrl}/wishlist?linked=error&message=Steam+authentication+failed");

        var (success, error) = await _oauth.HandleSteamCallbackAsync(state, claimedId);

        if (success)
            return Redirect($"{frontendUrl}/wishlist?linked=steam");

        return Redirect($"{frontendUrl}/wishlist?linked=error&message={Uri.EscapeDataString(error ?? "Unknown error")}");
    }

    /// <summary>
    /// Links a shop account by external ID directly.
    /// Steam: Steam64 ID (numeric). GOG: username. EGS: display name.
    /// </summary>
    [HttpPost("{shop}/link")]
    [Authorize]
    public async Task<IActionResult> LinkByExternalId(string shop, [FromBody] LinkAccountDto dto)
    {
        var shopId = ShopOAuthService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}. Supported: steam, gog, egs" });

        var (success, error) = await _oauth.LinkByExternalIdAsync(GetUserId(), shopId.Value, dto.ExternalId);
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
        var shopId = ShopOAuthService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}" });

        var removed = await _oauth.UnlinkAsync(GetUserId(), shopId.Value);
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
        var shopId = ShopOAuthService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}" });

        var linked = await _oauth.IsLinkedAsync(GetUserId(), shopId.Value);
        var profile = await _oauth.GetProfileAsync(GetUserId(), shopId.Value);

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
