using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface ICriteriaTreeService
{
    Task<GroupNode> GetRoot(CancellationToken ct = default);
    Task PersistAsync(GroupNode root, CancellationToken ct = default);
}