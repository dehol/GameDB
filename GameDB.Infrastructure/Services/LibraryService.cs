using GameDB.Core.DTOs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameDB.Infrastructure.Services;

public class LibraryService
{
    private readonly AppDbContext _db;
    public LibraryService(AppDbContext db) => _db = db;

    public async Task<List<UserLibraryRow>> GetLibraryAsync(int userId)
    {
        return await _db.Database
            .SqlQueryRaw<UserLibraryRow>("SELECT * FROM vw_user_library WHERE \"UserId\" = {0}", userId)
            .ToListAsync();
    }

    public async Task<string?> AddToLibraryAsync(int userId, int gameId, int shopId)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "CALL pr_add_to_library({0}, {1}, {2})",
                userId, gameId, shopId);
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.MessageText;
        }
    }
}
