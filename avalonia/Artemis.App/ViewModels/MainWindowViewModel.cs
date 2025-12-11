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

// using Avalonia.Controls.Models.TreeDataGrid;

namespace Artemis.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILocationRepository _locationRepo;
    private readonly ICriteriaTreeService _criteriaService;
    private readonly IDataFeedService _dataFeedService;
    public ObservableCollection<Location> LocationList { get; set; } = [];
    public ObservableCollection<Batch> BatchList { get; set; } = [];
    public ObservableCollection<GroupNode> Tree { get; set; } = [];
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
        set
        {
            if (_selectedLocation != value)
            {
                _selectedLocation = value;
                // SelectedScores.Clear();
                // if (_selectedLocation != null && Scores.TryGetValue(_selectedLocation.Id, out var scoreTree))
                //     SelectedScores.Add(scoreTree);
            }
        }
    }
    
    public MainWindowViewModel(ILocationRepository locationRepo, ICriteriaTreeService criteriaService, IDataFeedService dataFeedService)
    {
        _locationRepo = locationRepo;
        _criteriaService = criteriaService;
        _dataFeedService = dataFeedService;
        var criteriaToolbarEnabled = this.WhenAnyValue(vm => vm.SelectedNode)
            .Select(s => s != null)
            .DistinctUntilChanged();
        AddTermCommand = ReactiveCommand.CreateFromTask(AddTerm, criteriaToolbarEnabled);
        AddGroupCommand = ReactiveCommand.CreateFromTask(AddGroup, criteriaToolbarEnabled);
        RemoveNodeCommand = ReactiveCommand.CreateFromTask(RemoveNode, criteriaToolbarEnabled);
        AddLocationCommand = ReactiveCommand.CreateFromTask(AddLocation);
        UpdateLocationCommand = ReactiveCommand.CreateFromTask<Location>(UpdateLocation);
        RemoveLocationCommand = ReactiveCommand.CreateFromTask<Location>(RemoveLocationAsync);
        RefreshDataFeedsCommand = ReactiveCommand.CreateFromTask(RefreshDataFeedsAsync);
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
        await _dataFeedService.LoadOverpassPOI();
        BatchList.Clear();
        var batches = await _dataFeedService.ListBatchesAsync();
        foreach (var b in batches)
            BatchList.Add(b);
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
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
