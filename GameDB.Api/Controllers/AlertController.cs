using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertController : ControllerBase
{
    private readonly AlertService _alerts;
    public AlertController(AlertService alerts) => _alerts = alerts;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record CreateAlertDto(int GameId, decimal? TargetPrice, short? TargetDiscount);
    public record UpdateAlertDto(decimal? TargetPrice, short? TargetDiscount);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var alerts = await _alerts.GetUserAlertsAsync(GetUserId());
        return Ok(alerts);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAlertDto dto)
    {
        var error = await _alerts.SetAlertAsync(GetUserId(), dto.GameId, dto.TargetPrice, dto.TargetDiscount);
        if (error != null) return BadRequest(error);
        return Ok(new { message = "Alert created" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateAlertDto dto)
    {
        var (success, error) = await _alerts.UpdateAlertAsync(GetUserId(), id, dto.TargetPrice, dto.TargetDiscount);
        if (!success) return BadRequest(error);
        return Ok(new { message = "Alert updated" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _alerts.DeleteAlertAsync(GetUserId(), id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
