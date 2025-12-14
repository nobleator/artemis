using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IEvaluationService
{
    Task ScoreAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Score>> ListAsync(CancellationToken ct = default);
}
