using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface ICriteriaRepository
{
    Task<IEnumerable<Criteria>> ListAsync(CancellationToken ct = default);
    Task SaveTreeAsync(IEnumerable<Criteria> items, CancellationToken ct = default);
}
