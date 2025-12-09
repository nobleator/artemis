using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IBatchRepository
{
    Task<Batch> AddAsync(Batch batch, CancellationToken ct = default);
}
