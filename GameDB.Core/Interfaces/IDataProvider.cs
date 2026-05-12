using GameDB.Core.Models;

namespace GameDB.Core.Interfaces;

public interface IDataProvider
{
    string Name { get; }
    string Phase { get; }
    Task<int> UpdateOffersAsync(ImportJob job, CancellationToken ct);
}
