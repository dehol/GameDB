namespace GameDB.Core.Interfaces;

public interface IAuthService
{
    Task<string?> RegisterAsync(string username, string email, string password);
    Task<(string token, string username, string role)?> LoginAsync(string email, string password);
}