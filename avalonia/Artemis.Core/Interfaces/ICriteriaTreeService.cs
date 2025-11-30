using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface ICriteriaTreeService
{
    Task<GroupNode> GetRoot();
}