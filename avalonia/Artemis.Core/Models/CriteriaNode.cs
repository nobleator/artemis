namespace Artemis.Core.Models;

public abstract class CriteriaNode
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public List<CriteriaNode> Children { get; init; } = new();
}

public class GroupNode : CriteriaNode
{
    public OperatorType Operator { get; init; }
}

public class TermNode : CriteriaNode
{
    public string Category { get; init; } = "";
    public double Radius { get; init; }
}
