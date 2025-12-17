using System.Collections.ObjectModel;
using Artemis.Core.Models;

namespace Artemis.App.Models;

public abstract record ScoreTreeNode;
public record ScoreTreeLocationNode(int LocationId, ObservableCollection<ScoreTreeNode> Children) : ScoreTreeNode;
public record ScoreTreeCriteriaNode(CriteriaNode Node, ObservableCollection<ScoreTreeNode> Children) : ScoreTreeNode;
public record ScoreTreeScoreNode(Score Score) : ScoreTreeNode;
