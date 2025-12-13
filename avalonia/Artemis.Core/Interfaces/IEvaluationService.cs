using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IEvaluationService
{
    Task<EvaluationResult> ScoreAsync(Location location, CriteriaNode rootNode, CancellationToken ct = default);
}