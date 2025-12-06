using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Services;

public class CriteriaTreeService(ICriteriaRepository criteriaRepo) : ICriteriaTreeService
{
    private readonly ICriteriaRepository _criteriaRepo = criteriaRepo;

    public async Task PersistAsync(GroupNode root, CancellationToken ct = default)
    {
        var flat = new List<Criteria>();
        int counter = 1;

        void Walk(CriteriaNode node)
        {
            var left = counter++;
            foreach (var child in node.Children)
                Walk(child);
            var right = counter++;
            Criteria record = node switch
            {
                GroupNode g => new Criteria
                {
                    Id = g.Id,
                    Left = left,
                    Right = right,
                    Operator = (int)g.Operator,
                    CategoryId = null,
                    DistAmt = null
                },
                TermNode t => new Criteria
                {
                    Id = t.Id,
                    Left = left,
                    Right = right,
                    Operator = null,
                    CategoryId = t.CategoryId,
                    DistAmt = t.DistAmt
                },
                _ => throw new InvalidOperationException("Unknown node type")
            };
            flat.Add(record);
        }

        Walk(root);
        flat.Sort((a, b) => a.Left.CompareTo(b.Left));
        await _criteriaRepo.SaveTreeAsync(flat, ct);
    }

    public async Task<GroupNode> GetRoot(CancellationToken ct = default)
    {
        var rows = await _criteriaRepo.ListAsync(ct);
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

    public static bool InsertAfter(CriteriaNode root, int afterId, CriteriaNode newNode)
    {
        return InsertAfterInternal(null, root, afterId, newNode);
    }

    private static bool InsertAfterInternal(CriteriaNode? parent, CriteriaNode current, int afterId, CriteriaNode newNode)
    {
        if (current.Id == afterId)
        {
            if (current is GroupNode)
            {
                current.Children.Add(newNode);
            }
            else
            {
                if (parent is null)
                    throw new InvalidOperationException("Root has no parent; cannot insert sibling.");
                
                var siblings = parent.Children;
                var idx = siblings.IndexOf(siblings.Single(n => n.Id == afterId));
                if (idx < 0)
                    return false;

                siblings.Insert(idx + 1, newNode);
            }
            
            return true;
        }

        foreach (var child in current.Children)
        {
            if (InsertAfterInternal(current, child, afterId, newNode))
                return true;
        }

        return false;
    }

    public static bool RemoveAt(CriteriaNode root, int nodeId)
    {
        return RemoveAtInternal(null, root, nodeId);
    }

    private static bool RemoveAtInternal(CriteriaNode? parent, CriteriaNode current, int nodeId)
    {
        if (current.Id == nodeId)
        {
            if (parent is null)
                throw new InvalidOperationException("Cannot remove root.");

            var siblings = parent.Children;
            var idx = siblings.IndexOf(current);
            if (idx < 0)
                return false;

            siblings.RemoveAt(idx);
            return true;
        }

        foreach (var child in current.Children)
        {
            if (RemoveAtInternal(current, child, nodeId))
                return true;
        }

        return false;
    }
}