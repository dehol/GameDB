using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profile;
    public ProfileController(ProfileService profile) => _profile = profile;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record UpsertShopProfileDto(int ShopId, string ExternalUid);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await _profile.GetProfileAsync(GetUserId());
        if (user == null) return NotFound();

        return Ok(new
        {
            user.UserId,
            user.Username,
            user.Email,
            Role = user.Role.RoleName,
            user.CreatedAt,
            user.LastLogin,
            ShopProfiles = user.ShopProfiles.Select(sp => new
            {
                sp.ShopId,
                ShopName = sp.Shop.Name,
                sp.ExternalUid,
                sp.LinkedAt
            })
        });
    }

    /// <summary>
    /// Links a shop account by external ID.
    /// Steam: Steam64 ID, GOG: username.
    /// </summary>
    [HttpPut("shop-profile")]
    public async Task<IActionResult> UpsertShopProfile(UpsertShopProfileDto dto)
    {
        var (success, error) = await _profile.UpsertShopProfileAsync(GetUserId(), dto.ShopId, dto.ExternalUid);
        if (!success) return BadRequest(new { error });
        return Ok(new { message = "Shop profile linked" });
    }

    /// <summary>
    /// Unlinks a shop account.
    /// </summary>
    [HttpDelete("shop-profile/{shopId}")]
    public async Task<IActionResult> UnlinkShopProfile(int shopId)
    {
        var (success, error) = await _profile.UnlinkShopProfileAsync(GetUserId(), shopId);
        if (!success) return BadRequest(new { error });
        return Ok(new { message = "Shop profile unlinked" });
    }
}
