using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Services;

public class CriteriaTreeService(ICriteriaRepository criteriaRepo) : ICriteriaTreeService
{
    private readonly ICriteriaRepository _criteriaRepo = criteriaRepo;

    public async Task<GroupNode> GetRoot()
    {
        var rows = await _criteriaRepo.ListAsync();
        var stack = new Stack<(CriteriaNode node, int rgt)>();
        GroupNode? root = null;

        foreach (var r in rows)
        {
            CriteriaNode node = r.Operator is not null
                ? new GroupNode(r.Id, (OperatorType)r.Operator.Value)
                : new TermNode(r.Id, r.CategoryId!.Value, r.DistAmt!.Value);

            while (stack.Count > 0 && r.Left > stack.Peek().rgt)
                stack.Pop();
            if (stack.Count == 0)
                root = node as GroupNode;
            else
                ((GroupNode)stack.Peek().node).Children.Add(node);
            stack.Push((node, r.Right));
        }

        if (root == null)
            throw new InvalidOperationException("No nodes.");

        return root;
    }
}