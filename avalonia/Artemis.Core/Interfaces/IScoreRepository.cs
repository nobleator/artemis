using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IScoreRepository
{
    Task SaveTreeAsync(int locationId, IEnumerable<Score> items, CancellationToken ct = default);
    Task<IEnumerable<Score>> ListAsync(CancellationToken ct = default);
}
