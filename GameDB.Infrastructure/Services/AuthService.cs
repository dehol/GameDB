using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GameDB.Infrastructure.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    
    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<string?> RegisterAsync(string username, string email, string password)
    {
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return "Email вже використовується";
        if (await _db.Users.AnyAsync(u => u.Username == username))
            return "Ім'я користувача вже зайняте";

        // Check if this is the first user OR specific email - make them admin
        var isFirstUser = !await _db.Users.AnyAsync();
        var isAdminEmail = email.ToLower() == "smutokdanilo56@gmail.com";
        var userRole = (isFirstUser || isAdminEmail)
            ? await _db.Roles.FirstAsync(r => r.RoleName == "admin")
            : await _db.Roles.FirstAsync(r => r.RoleName == "user");

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RoleId = userRole.RoleId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return null; // null = успіх
    }

    public async Task<(string token, string username, string role)?> LoginAsync(string email, string password)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = GenerateToken(user);
        return (token, user.Username, user.Role.RoleName);
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.RoleName),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:ExpiresInDays"]!)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}