using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IBatchRepository
{
    Task<Batch> AddAsync(Batch batch, CancellationToken ct = default);
    Task<Batch> UpdateAsync(Batch batch, CancellationToken ct = default);
    Task<IEnumerable<Batch>> ListAsync(CancellationToken ct = default);
}
