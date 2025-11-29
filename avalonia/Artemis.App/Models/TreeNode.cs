using System.Collections.ObjectModel;

namespace Artemis.App.Models;

public class TreeNode
{
    public required string Name { get; set; }
    public ObservableCollection<TreeNode> Children { get; set; } = [];
}