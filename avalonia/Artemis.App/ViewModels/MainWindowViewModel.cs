using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Artemis.Core.Models;
using Artemis.Core.Interfaces;
using Avalonia.Logging;
using ReactiveUI;
using System.Reactive;
using Artemis.App.Models;


// using Avalonia.Controls.Models.TreeDataGrid;
// using Location = Artemis.Core.Models.Location;

namespace Artemis.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILocationRepository _locationRepo;
    public ObservableCollection<Location> LocationList { get; set; } = [];
    public ObservableCollection<TreeNode> Tree { get; set; } = [];
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
    
    public MainWindowViewModel(ILocationRepository locationRepo)
    {
        _locationRepo = locationRepo;
        AddLocationCommand = ReactiveCommand.CreateFromTask(AddLocation);
        UpdateLocationCommand = ReactiveCommand.CreateFromTask<Location>(UpdateLocation);
        RemoveLocationCommand = ReactiveCommand.CreateFromTask<Location>(RemoveLocationAsync);
        Tree =
        [
            new TreeNode
            {
                Name = "Root",
                Children =
                {
                    new TreeNode { Name = "Child 1" },
                    new TreeNode { Name = "Child 2" }
                }
            }
        ];
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
    
    public async Task InitializeAsync()
    {
        try
        {
            var locations = await _locationRepo.ListAsync();
            foreach (var loc in locations)
            {
                LocationList.Add(loc);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
