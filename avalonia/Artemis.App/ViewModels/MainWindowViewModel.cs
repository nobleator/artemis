using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Artemis.Core.Models;
using Artemis.Core.Interfaces;
using Avalonia.Logging;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using Artemis.Core.Services;
using System.Linq;
using Avalonia.Controls;
using Location = Artemis.Core.Models.Location;
using Avalonia.Controls.Models.TreeDataGrid;
using System.Collections.Generic;

namespace Artemis.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILocationRepository _locationRepo;
    private readonly ICriteriaTreeService _criteriaService;
    private readonly IDataFeedService _dataFeedService;
    private readonly IEvaluationService _evalService;
    public ObservableCollection<Location> LocationList { get; set; } = [];
    public ObservableCollection<Batch> BatchList { get; set; } = [];
    public ObservableCollection<GroupNode> Tree { get; set; } = [];
    public HierarchicalTreeDataGridSource<ScoreTreeNode> ScoreTree { get; }
    private readonly ObservableCollection<ScoreTreeNode> _scoreRoots = [];
    
    private double _batchRunProgress;

    public double BatchRunProgress
    {
        get => _batchRunProgress;
        set => this.RaiseAndSetIfChanged(ref _batchRunProgress, value);
    }
    private CriteriaNode? _selectedNode;
    public CriteriaNode? SelectedNode
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
                TermNode t => $"Term {t.CategoryId}",
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
        AddTermCommand = ReactiveCommand.CreateFromTask(AddTerm, criteriaToolbarEnabled);
        AddGroupCommand = ReactiveCommand.CreateFromTask(AddGroup, criteriaToolbarEnabled);
        RemoveNodeCommand = ReactiveCommand.CreateFromTask(RemoveNode, criteriaToolbarEnabled);
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
    }
    
    public ReactiveCommand<Unit, Unit> AddTermCommand { get; }
    private async Task AddTerm()
    {
        if (SelectedNode?.Id != null)
        {
            CriteriaTreeService.InsertAfter(Tree.First(), SelectedNode.Id, new TermNode(-1, (int)Category.Airport, 0));
            await _criteriaService.PersistAsync(Tree.First());
            var root = await _criteriaService.GetRoot();
            Tree.Clear();
            Tree.Add(root);
        }
    }
    
    public ReactiveCommand<Unit, Unit> AddGroupCommand { get; }
    private async Task AddGroup()
    {
        if (SelectedNode?.Id != null)
        {
            CriteriaTreeService.InsertAfter(Tree.First(), SelectedNode.Id, new GroupNode(-1, OperatorType.And));
            await _criteriaService.PersistAsync(Tree.First());
            var root = await _criteriaService.GetRoot();
            Tree.Clear();
            Tree.Add(root);
        }
    }
    
    public ReactiveCommand<Unit, Unit> RemoveNodeCommand { get; }
    private async Task RemoveNode()
    {
        if (SelectedNode?.Id != null)
        {
            CriteriaTreeService.RemoveAt(Tree.First(), SelectedNode.Id);
            await _criteriaService.PersistAsync(Tree.First());
            var root = await _criteriaService.GetRoot();
            Tree.Clear();
            Tree.Add(root);
        }
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
            foreach (var childNode in BuildCriteriaTree(root, locationScores))
                locChildren.Add(childNode);
            var locNode = new ScoreTreeLocationNode(loc.Id, locChildren);
            _scoreRoots.Add(locNode);
        }
        Console.WriteLine("Score calculation complete.");
    }

    private static IEnumerable<ScoreTreeNode> BuildCriteriaTree(CriteriaNode criteriaNode, List<Score> scoresForLocation)
    {
        var children = new ObservableCollection<ScoreTreeNode>(
            criteriaNode.Children.SelectMany(c => BuildCriteriaTree(c, scoresForLocation))
        );
        var currentNode = new ScoreTreeCriteriaNode(criteriaNode, children);
        if (!criteriaNode.Children.Any())
        {
            foreach (var s in scoresForLocation.Where(s => s.CriteriaId == criteriaNode.Id))
                children.Add(new ScoreTreeScoreNode(s));
        }

        yield return currentNode;
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
            var root = await _criteriaService.GetRoot();
            Tree.Add(root);
            var scores = await _evalService.ListAsync();
            _scoreRoots.Clear();
            var scoresByLocation = scores
                .GroupBy(s => s.LocationId)
                .ToDictionary(g => g.Key, g => g.ToList());
            foreach (var loc in locations)
            {
                var locChildren = new ObservableCollection<ScoreTreeNode>();
                var locationScores = scoresByLocation.TryGetValue(loc.Id, out var list) ? list : [];
                foreach (var childNode in BuildCriteriaTree(root, locationScores))
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
