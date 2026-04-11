using GameDB.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameDB.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    public record RegisterDto(string Username, string Email, string Password);
    public record LoginDto(string Email, string Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest("Пароль мінімум 6 символів");

        var error = await _auth.RegisterAsync(dto.Username, dto.Email, dto.Password);
        if (error != null) return BadRequest(error);
        return Ok(new { message = "Реєстрація успішна" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await _auth.LoginAsync(dto.Email, dto.Password);
        if (result == null) return Unauthorized("Невірний email або пароль");

        return Ok(new
        {
            token = result.Value.token,
            username = result.Value.username,
            role = result.Value.role
        });
    }
}
