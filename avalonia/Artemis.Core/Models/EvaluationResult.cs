using System.Collections.ObjectModel;

namespace Artemis.Core.Models;

public record EvaluationResult(
    CriteriaNode Node,
    double Score,
    ObservableCollection<EvaluationResult> Children // TODO Core model vs App model
);
