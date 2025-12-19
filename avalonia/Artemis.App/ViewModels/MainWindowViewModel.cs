using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Artemis.App.Models;
using Artemis.Core.Models;
using Artemis.Core.Interfaces;
using Avalonia.Logging;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Linq;
using Avalonia.Controls;
using Location = Artemis.Core.Models.Location;
using Avalonia.Controls.Models.TreeDataGrid;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace Artemis.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILocationRepository _locationRepo;
    private readonly ICriteriaTreeService _criteriaService;
    private readonly IDataFeedService _dataFeedService;
    private readonly IEvaluationService _evalService;
    public ObservableCollection<Location> LocationList { get; set; } = [];
    public ObservableCollection<Batch> BatchList { get; set; } = [];
    public ObservableCollection<CriteriaTreeGroupNode> Tree { get; set; } = [];
    private IDisposable? _persistSubscription;
    private readonly Subject<Unit> _criteriaChanged = new();
    public HierarchicalTreeDataGridSource<ScoreTreeNode> ScoreTree { get; }
    private readonly ObservableCollection<ScoreTreeNode> _scoreRoots = [];
    
    private double _batchRunProgress;

    public double BatchRunProgress
    {
        get => _batchRunProgress;
        set => this.RaiseAndSetIfChanged(ref _batchRunProgress, value);
    }
    private CriteriaTreeCriteriaNode? _selectedNode;
    public CriteriaTreeCriteriaNode? SelectedNode
    {
        get => _selectedNode;
        set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
    }
    private Location? _selectedLocation;
    public Location? SelectedLocation
    {
        get => _selectedLocation;
        set => this.RaiseAndSetIfChanged(ref _selectedLocation, value);
    }
    
    static string GetText(ScoreTreeNode x) =>
        x switch
        {
            ScoreTreeLocationNode l => $"Location {l.LocationId}",
            ScoreTreeCriteriaNode c => c.Node switch
            {
                GroupNode g => g.Operator.ToString(),
                TermNode t => $"Term {t.Category}",
                _ => ""
            },
            ScoreTreeScoreNode s => s.Score.NormalizedValue.ToString("0.00"),
            _ => ""
        };

    public MainWindowViewModel(ILocationRepository locationRepo, ICriteriaTreeService criteriaService, IDataFeedService dataFeedService, IEvaluationService evalService)
    {
        _locationRepo = locationRepo;
        _criteriaService = criteriaService;
        _dataFeedService = dataFeedService;
        _evalService = evalService;
        BatchRunProgress = 100;
        var criteriaToolbarEnabled = this.WhenAnyValue(vm => vm.SelectedNode)
            .Select(s => s != null)
            .DistinctUntilChanged();
        var locationToolbarEnabled = this.WhenAnyValue(vm => vm.SelectedLocation)
            .Select(s => s != null)
            .DistinctUntilChanged();
        AddTermCommand = ReactiveCommand.Create(AddTerm, criteriaToolbarEnabled);
        AddGroupCommand = ReactiveCommand.Create(AddGroup, criteriaToolbarEnabled);
        RemoveNodeCommand = ReactiveCommand.Create(RemoveNode, criteriaToolbarEnabled);
        AddLocationCommand = ReactiveCommand.CreateFromTask(AddLocation);
        UpdateLocationCommand = ReactiveCommand.CreateFromTask<Location>(UpdateLocation);
        RemoveLocationCommand = ReactiveCommand.CreateFromTask<Location>(RemoveLocationAsync);
        RefreshDataFeedsCommand = ReactiveCommand.CreateFromTask(RefreshDataFeedsAsync);
        CalculateScoresCommand = ReactiveCommand.CreateFromTask(CalculateScoresAsync);
        ScoreTree = new HierarchicalTreeDataGridSource<ScoreTreeNode>(_scoreRoots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<ScoreTreeNode>(
                    new TextColumn<ScoreTreeNode, string>("Item", x => GetText(x)),
                    x => x switch
                    {
                        ScoreTreeLocationNode l => l.Children,
                        ScoreTreeCriteriaNode c => c.Children,
                        _ => null
                    })
            }
        };
        _persistSubscription = _criteriaChanged
            .Throttle(TimeSpan.FromMilliseconds(500))
            .SelectMany(async _ =>
            {
                if (Tree.Any())
                {
                    var uiRoot = Tree.First();
                    var domainRoot = ToDomain(uiRoot);
                    await _criteriaService.PersistAsync((GroupNode)domainRoot);
                }
                return Unit.Default;
            })
            .Subscribe();
    }

    private static CriteriaTreeCriteriaNode ToUi(CriteriaNode node)
    {
        CriteriaTreeCriteriaNode uiNode =
            node switch
            {
                GroupNode g => new CriteriaTreeGroupNode(g.Id, g.Operator),
                TermNode t => new CriteriaTreeTermNode(t.Id, t.Category, t.DistAmt),
                _ => throw new InvalidOperationException()
            };

        foreach (var child in node.Children)
            uiNode.Children.Add(ToUi(child));

        return uiNode;
    }

    private static CriteriaNode ToDomain(CriteriaTreeCriteriaNode node)
    {
        CriteriaNode domainNode =
            node switch
            {
                CriteriaTreeGroupNode g => new GroupNode(g.Id, g.Operator),
                CriteriaTreeTermNode t => new TermNode(t.Id, t.Category, t.DistAmt),
                _ => throw new InvalidOperationException()
            };

        foreach (var child in node.Children)
            domainNode.Children.Add(ToDomain(child));

        return domainNode;
    }
    
    public ReactiveCommand<Unit, Unit> AddTermCommand { get; }
    private void AddTerm()
    {
        if (SelectedNode is null) return;
        var termNode = new CriteriaTreeTermNode(-1, Category.Airport, 0);
        SelectedNode.Children.Add(termNode);
    }
    
    public ReactiveCommand<Unit, Unit> AddGroupCommand { get; }
    private void AddGroup()
    {
        if (SelectedNode is null) return;
        var groupNode = new CriteriaTreeGroupNode(-1, OperatorType.And);
        SelectedNode.Children.Add(groupNode);
    }

    private static CriteriaTreeCriteriaNode? FindParent(CriteriaTreeCriteriaNode current, CriteriaTreeCriteriaNode child)
    {
        if (current.Children.Contains(child)) return current;
        foreach (var c in current.Children)
        {
            var result = FindParent(c, child);
            if (result != null) return result;
        }
        return null;
    }
    
    public ReactiveCommand<Unit, Unit> RemoveNodeCommand { get; }
    private void RemoveNode()
    {
        if (SelectedNode is null) return;
        var parent = FindParent(Tree.First(), SelectedNode);
        if (parent is null) return;
        parent.Children.Remove(SelectedNode);
        SelectedNode = parent;
    }
    
    public ReactiveCommand<Unit, Unit> AddLocationCommand { get; }
    private async Task AddLocation()
    {
        var loc = await _locationRepo.AddAsync(new Location
        {
            Id = -1,
            Name = "New Location",
            Address = "Unknown"
        });
        LocationList.Add(loc);
    }
    
    public ReactiveCommand<Location, Unit> UpdateLocationCommand { get; }
    private async Task UpdateLocation(Location loc)
    {
        var updated = await _locationRepo.UpdateAsync(loc);
        var idx = LocationList.IndexOf(loc);
        if (idx < 0) return;
        LocationList[idx] = updated;
    }

    public ReactiveCommand<Location, Unit> RemoveLocationCommand { get; }
    private async Task RemoveLocationAsync(Location loc)
    {
        Console.WriteLine($"Remove {loc.Id}");
        var rows = await _locationRepo.DeleteAsync(loc.Id);
        Console.WriteLine($"Deleted {rows} rows");
        LocationList.Remove(loc);
    }

    public ReactiveCommand<Unit, Unit> RefreshDataFeedsCommand { get; }
    private async Task RefreshDataFeedsAsync()
    {
        var progress = new Progress<double>(v => BatchRunProgress = v);
        await _dataFeedService.LoadOverpassPOI(progress);
        BatchList.Clear();
        var batches = await _dataFeedService.ListBatchesAsync();
        foreach (var b in batches)
            BatchList.Add(b);
    }

    public ReactiveCommand<Unit, Unit> CalculateScoresCommand { get; }
    private async Task CalculateScoresAsync()
    {
        Console.WriteLine("Calculating scores...");
        await _evalService.ScoreAllAsync();
        var locations = await _locationRepo.ListAsync();
        var root = await _criteriaService.GetRoot();
        var scores = await _evalService.ListAsync();
        _scoreRoots.Clear();
        var scoresByLocation = scores
            .GroupBy(s => s.LocationId)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var loc in locations)
        {
            var locChildren = new ObservableCollection<ScoreTreeNode>();
            var locationScores = scoresByLocation.TryGetValue(loc.Id, out var list) ? list : [];
            foreach (var childNode in BuildScoreTree(root, locationScores))
                locChildren.Add(childNode);
            var locNode = new ScoreTreeLocationNode(loc.Id, locChildren);
            _scoreRoots.Add(locNode);
        }
        Console.WriteLine("Score calculation complete.");
    }

    private static IEnumerable<ScoreTreeNode> BuildScoreTree(CriteriaNode criteriaNode, List<Score> scoresForLocation)
    {
        var children = new ObservableCollection<ScoreTreeNode>(
            criteriaNode.Children.SelectMany(c => BuildScoreTree(c, scoresForLocation))
        );
        var currentNode = new ScoreTreeCriteriaNode(criteriaNode, children);
        if (!criteriaNode.Children.Any())
        {
            foreach (var s in scoresForLocation.Where(s => s.CriteriaId == criteriaNode.Id))
                children.Add(new ScoreTreeScoreNode(s));
        }

        yield return currentNode;
    }

    private void ObserveCriteriaTree(CriteriaTreeCriteriaNode node)
    {
        node.PropertyChanged += (_, __) => _criteriaChanged.OnNext(Unit.Default);
        node.Children.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (CriteriaTreeCriteriaNode c in e.NewItems)
                    ObserveCriteriaTree(c);

            _criteriaChanged.OnNext(Unit.Default);
        };

        foreach (var child in node.Children)
            ObserveCriteriaTree(child);
    }
    
    public async Task InitializeAsync()
    {
        try
        {
            var batches = await _dataFeedService.ListBatchesAsync();
            foreach (var b in batches)
                BatchList.Add(b);
            var locations = await _locationRepo.ListAsync();
            foreach (var loc in locations)
                LocationList.Add(loc);
            var domainRoot = await _criteriaService.GetRoot();
            var uiRoot = ToUi(domainRoot);
            Tree.Clear();
            Tree.Add((CriteriaTreeGroupNode)uiRoot);
            ObserveCriteriaTree(uiRoot);
            var scores = await _evalService.ListAsync();
            _scoreRoots.Clear();
            var scoresByLocation = scores
                .GroupBy(s => s.LocationId)
                .ToDictionary(g => g.Key, g => g.ToList());
            foreach (var loc in locations)
            {
                var locChildren = new ObservableCollection<ScoreTreeNode>();
                var locationScores = scoresByLocation.TryGetValue(loc.Id, out var list) ? list : [];
                foreach (var childNode in BuildScoreTree(domainRoot, locationScores))
                    locChildren.Add(childNode);
                var locNode = new ScoreTreeLocationNode(loc.Id, locChildren);
                _scoreRoots.Add(locNode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
