using System.Collections.ObjectModel;

namespace Artemis.Core.Models;

public abstract record CriteriaNode(int Id)
{
    public ObservableCollection<CriteriaNode> Children { get; } = [];
    public bool IsExpanded { get; set; } = true;
}

public record GroupNode(int Id, OperatorType Operator) : CriteriaNode(Id)
{
    public OperatorType[] OperatorTypeValues { get; } = Enum.GetValues<OperatorType>();
}
public record TermNode(int Id, int CategoryId, double DistAmt) : CriteriaNode(Id);
