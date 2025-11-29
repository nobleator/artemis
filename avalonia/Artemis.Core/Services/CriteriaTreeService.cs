using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Services;

public class CriteriaTreeService : ICriteriaTreeService
{
    private ICriteriaRepository _criteriaRepo;
    public CriteriaTreeService(ICriteriaRepository criteriaRepo)
    {
        _criteriaRepo = criteriaRepo;
    }

    public async Task<CriteriaNode> GetRoot()
    {
        var rows = (await _criteriaRepo.ListAsync()).OrderBy(x => x.Left).ToList();
        var groupLookup = (await _criteriaRepo.ListGroupsAsync()).ToDictionary(g => g.Id);
        var termLookup = (await _criteriaRepo.ListTermsAsync()).ToDictionary(g => g.Id);
        
        // var rows = _nestedSetRepo.GetAllOrderedByLeft();
        // var groupLookup = _groupRepo.GetAll().ToDictionary(g => g.NodeId);
        // var termLookup  = _termRepo.GetAll().ToDictionary(t => t.NodeId);
        var stack = new Stack<(Criteria Row, CriteriaNode Node)>();
        CriteriaNode? root = null;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            CriteriaNode node =
                groupLookup.TryGetValue(row.Id, out var g) ? g
                : termLookup.TryGetValue(row.Id, out var t) ? t
                : throw new InvalidOperationException($"Missing payload for {row.Id}");

            // Attach to parent
            if (stack.Count > 0)
                stack.Peek().Node.Children.Add(node);
            else
                root = node;

            // Push
            stack.Push((row, node));

            // Peek ahead to next row to see which nodes to close
            int nextLft = (i + 1 < rows.Count) ? rows[i + 1].Left : int.MaxValue;

            // Pop any nodes whose RGT is less than next LFT
            while (stack.Count > 0 && nextLft > stack.Peek().Row.Right)
                stack.Pop();
        }

        return root ?? throw new InvalidOperationException("Tree is empty");
    }
}