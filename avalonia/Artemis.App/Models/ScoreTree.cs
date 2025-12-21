using System.Collections.ObjectModel;
using Artemis.Core.Models;

namespace Artemis.App.Models;

public abstract record ScoreTreeNode(double Score);
public record ScoreTreeLocationNode(int LocationId, double Score, ObservableCollection<ScoreTreeNode> Children) : ScoreTreeNode(Score);
public record ScoreTreeCriteriaNode(CriteriaNode Node, double Score, ObservableCollection<ScoreTreeNode> Children) : ScoreTreeNode(Score);
