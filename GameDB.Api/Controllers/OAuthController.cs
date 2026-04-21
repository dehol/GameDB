using GameDB.Core.Configuration;
using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

/// <summary>
/// Handles OAuth authentication flows for shop accounts (Steam, GOG, Epic Games Store).
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
    /// Initiates OAuth flow for the specified shop.
    /// Returns the authorization URL that the frontend should redirect the user to.
    /// </summary>
    [HttpGet("{shop}/authorize")]
    [Authorize]
    public IActionResult Authorize(string shop)
    {
        var shopId = ShopOAuthService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}. Supported: steam, gog, egs" });

        var (url, state) = _oauth.GetAuthorizationUrl(GetUserId(), shopId.Value);
        return Ok(new { url, state });
    }

    /// <summary>
    /// Handles OAuth callback from the shop.
    /// For Steam: receives OpenID response with claimed_id.
    /// For GOG/EGS: receives authorization code.
    /// </summary>
    [HttpGet("{shop}/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(string shop, [FromQuery] string? state, [FromQuery] string? code,
        // Steam OpenID params
        [FromQuery] string? openid_claimed_id,
        [FromQuery] string? openid_mode,
        [FromQuery] string? openid_ns)
    {
        var shopId = ShopOAuthService.ShopSlugToId(shop);
        if (shopId == null)
            return BadRequest(new { error = $"Unknown shop: {shop}" });

        var frontendUrl = _oauthSettings.FrontendBaseUrl;

        try
        {
            bool success;
            string? error;

            if (shopId == 1)
            {
                // Steam OpenID callback
                if (string.IsNullOrWhiteSpace(openid_claimed_id))
                {
                    return Redirect($"{frontendUrl}/wishlist?linked=error&message=Steam+authentication+failed");
                }

                (success, error) = await _oauth.HandleCallbackAsync(state ?? "", openid_claimed_id);
            }
            else
            {
                // GOG/EGS OAuth callback
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                {
                    return Redirect($"{frontendUrl}/wishlist?linked=error&message=Authorization+code+missing");
                }

                (success, error) = await _oauth.HandleCallbackAsync(state, code);
            }

            if (success)
            {
                return Redirect($"{frontendUrl}/wishlist?linked={shop}");
            }
            else
            {
                return Redirect($"{frontendUrl}/wishlist?linked=error&message={Uri.EscapeDataString(error ?? "Unknown error")}");
            }
        }
        catch (Exception ex)
        {
            return Redirect($"{frontendUrl}/wishlist?linked=error&message={Uri.EscapeDataString(ex.Message)}");
        }
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

        var linked = await _oauth.HasValidAuthAsync(GetUserId(), shopId.Value);
        var profile = await _oauth.GetProfileAsync(GetUserId(), shopId.Value);

        return Ok(new
        {
            linked,
            shopId = shopId.Value,
            shopName = shop,
            externalUid = profile?.ExternalUid
        });
    }
}
