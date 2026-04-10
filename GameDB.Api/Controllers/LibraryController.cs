using GameDB.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LibraryController : ControllerBase
{
    private readonly ILibraryService _library;
    public LibraryController(ILibraryService library) => _library = library;

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
}
