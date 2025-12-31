using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IEvaluationService
{
    Task ScoreAllAsync(CancellationToken ct = default);
    Task<EvaluationResult> ScoreAsync(Location location, CriteriaNode node, IDictionary<int, EvaluationResult> sink, CancellationToken ct = default);
    Task<IEnumerable<Score>> ListAsync(CancellationToken ct = default);
}
