namespace Artemis.Core.Models;

public abstract record CriteriaNode(int Id)
{
    public List<CriteriaNode> Children { get; } = [];
}

public record GroupNode(int Id, OperatorType Operator) : CriteriaNode(Id)
{
    public OperatorType[] OperatorTypeValues { get; } = Enum.GetValues<OperatorType>();
}
public record TermNode(int Id, int CategoryId, decimal DistAmt) : CriteriaNode(Id);
