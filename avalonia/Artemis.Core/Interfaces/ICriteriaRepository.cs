using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface ICriteriaRepository
{
    Task<Criteria?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<Criteria>> ListAsync(CancellationToken ct = default);
    Task<Criteria> AddAsync(Criteria criteria, CancellationToken ct = default);
    Task<Criteria> UpdateAsync(Criteria criteria, CancellationToken ct = default);
    Task<int> DeleteAsync(int id, CancellationToken ct = default);
}
