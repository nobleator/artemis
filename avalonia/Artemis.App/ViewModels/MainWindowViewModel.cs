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
    private readonly ILocationService _locationService;
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
    
    private static string GetLocationText(ScoreTreeNode x) =>
        x switch
        {
            ScoreTreeLocationNode l => $"Location {l.LocationId}",
            ScoreTreeCriteriaNode c => c.Node switch
            {
                GroupNode g => g.Operator.ToString(),
                _ => ""
            },
            _ => ""
        };
    private static string GetCategoryText(ScoreTreeNode x) =>
        x switch
        {
            ScoreTreeCriteriaNode c => c.Node switch
            {
                TermNode t => $"{t.Category}",
                GroupNode g => $"{g.Children.Count} children",
                _ => ""
            },
            _ => ""
        };
    private static string GetScoreText(ScoreTreeNode x) => x.Score.ToString("0.00");

    public MainWindowViewModel(ILocationRepository locationRepo, ICriteriaTreeService criteriaService, IDataFeedService dataFeedService, IEvaluationService evalService, ILocationService locationService)
    {
        _locationRepo = locationRepo;
        _criteriaService = criteriaService;
        _dataFeedService = dataFeedService;
        _evalService = evalService;
        _locationService = locationService;
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
        AddLocationCommand = ReactiveCommand.CreateFromTask(AddLocationAsync);
        SaveAllLocationsCommand = ReactiveCommand.CreateFromTask(SaveAllLocationsAsync);
        RemoveLocationCommand = ReactiveCommand.CreateFromTask(RemoveLocationAsync, locationToolbarEnabled);
        GeocodeLocationCommand = ReactiveCommand.CreateFromTask(GeocodeLocationAsync, locationToolbarEnabled);
        RefreshDataFeedsCommand = ReactiveCommand.CreateFromTask(RefreshDataFeedsAsync);
        CalculateScoresCommand = ReactiveCommand.CreateFromTask(CalculateScoresAsync);
        ScoreTree = new HierarchicalTreeDataGridSource<ScoreTreeNode>(_scoreRoots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<ScoreTreeNode>(
                    new TextColumn<ScoreTreeNode, string>("Location", x => GetLocationText(x)),
                    x => x switch
                    {
                        ScoreTreeLocationNode l => l.Children,
                        ScoreTreeCriteriaNode c => c.Children,
                        _ => null
                    }),
                new TextColumn<ScoreTreeNode, string>("Category", x => GetCategoryText(x)),
                new TextColumn<ScoreTreeNode, string>("Score", x => GetScoreText(x))
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
    private async Task AddLocationAsync()
    {
        var loc = await _locationRepo.AddAsync(new Location
        {
            Id = -1,
            Name = "New Location",
            Address = "Unknown"
        });
        LocationList.Add(loc);
    }
    
    public ReactiveCommand<Unit, Unit> SaveAllLocationsCommand { get; }
    private async Task SaveAllLocationsAsync()
    {
        for (var idx = 0; idx < LocationList.Count; idx++)
        {
            var updated = await _locationRepo.UpdateAsync(LocationList[idx]);
            LocationList[idx] = updated;
        }
    }

    public ReactiveCommand<Unit, Unit> RemoveLocationCommand { get; }
    private async Task RemoveLocationAsync()
    {
        if (SelectedLocation is null) return; 
        Console.WriteLine($"Remove {SelectedLocation.Id}");
        var rows = await _locationRepo.DeleteAsync(SelectedLocation.Id);
        Console.WriteLine($"Deleted {rows} rows");
        LocationList.Remove(SelectedLocation);
    }

    public ReactiveCommand<Unit, Unit> GeocodeLocationCommand { get; }
    private async Task GeocodeLocationAsync()
    {
        if (SelectedLocation is null) return;
        var updated = await _locationService.GeocodeAsync(SelectedLocation);
        var idx = LocationList.IndexOf(SelectedLocation);
        if (idx < 0) return;
        LocationList[idx] = updated;
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
        Console.WriteLine("Updating view model...");
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
            var rootScore = locationScores.Single(x => x.CriteriaId == 1);
            var locNode = new ScoreTreeLocationNode(loc.Id, rootScore.NormalizedValue, locChildren);
            _scoreRoots.Add(locNode);
        }
        Console.WriteLine("Score update complete.");
    }

    private static IEnumerable<ScoreTreeNode> BuildScoreTree(CriteriaNode criteriaNode, List<Score> scoresForLocation)
    {
        var children = new ObservableCollection<ScoreTreeNode>(
            criteriaNode.Children.SelectMany(c => BuildScoreTree(c, scoresForLocation))
        );
        var score = scoresForLocation.Single(x => x.CriteriaId == criteriaNode.Id);
        var currentNode = new ScoreTreeCriteriaNode(criteriaNode, score.NormalizedValue, children);
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
                var rootScore = locationScores.Single(x => x.CriteriaId == 1);
                var locNode = new ScoreTreeLocationNode(loc.Id, rootScore.NormalizedValue, locChildren);
                _scoreRoots.Add(locNode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
