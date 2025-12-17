using Artemis.Core.Models;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;

namespace Artemis.App.Models;

public abstract class CriteriaTreeCriteriaNode(int id) : ReactiveObject
{
    public int Id { get; } = id;

    public ObservableCollection<CriteriaTreeCriteriaNode> Children { get; } = [];

    bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}
public sealed class CriteriaTreeGroupNode(int id, OperatorType op) : CriteriaTreeCriteriaNode(id)
{
    public OperatorType[] OperatorTypeValues { get; } = Enum.GetValues<OperatorType>();
    OperatorType _operator = op;
    public OperatorType Operator
    {
        get => _operator;
        set => this.RaiseAndSetIfChanged(ref _operator, value);
    }
}
public sealed class CriteriaTreeTermNode(int id, Category cat, double dist) : CriteriaTreeCriteriaNode(id)
{
    public Category[] CategoryValues { get; } = Enum.GetValues<Category>();
    Category _category = cat;
    public Category Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }

    double _distAmt = dist;
    public double DistAmt
    {
        get => _distAmt;
        set => this.RaiseAndSetIfChanged(ref _distAmt, value);
    }
}
