namespace Artemis.Core.Models;

public abstract record CriteriaNode(int Id)
{
    public List<CriteriaNode> Children { get; } = [];
}

public record GroupNode(int Id, OperatorType Operator) : CriteriaNode(Id);
public record TermNode(int Id, Category Category, double DistAmt) : CriteriaNode(Id);
