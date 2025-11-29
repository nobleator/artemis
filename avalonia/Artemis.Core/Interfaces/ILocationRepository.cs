using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<Location>> ListAsync(CancellationToken ct = default);
    Task<Location> AddAsync(Location location, CancellationToken ct = default);
    Task<Location> UpdateAsync(Location location, CancellationToken ct = default);
    Task<int> DeleteAsync(int id, CancellationToken ct = default);
}
