using ReactiveUI;

namespace Artemis.App.Models;

public class Location : ReactiveObject
{
    int _id;
    string? _name;
    string? _address;
    double? _latitude;
    double? _longitude;
    string? _notes;

    public int Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public string? Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string? Address
    {
        get => _address;
        set => this.RaiseAndSetIfChanged(ref _address, value);
    }

    public double? Latitude
    {
        get => _latitude;
        set
        {
            this.RaiseAndSetIfChanged(ref _latitude, value);
            this.RaisePropertyChanged(nameof(IsGeocoded));
        }
    }

    public double? Longitude
    {
        get => _longitude;
        set
        {
            this.RaiseAndSetIfChanged(ref _longitude, value);
            this.RaisePropertyChanged(nameof(IsGeocoded));
        }
    }

    public string? Notes
    {
        get => _notes;
        set => this.RaiseAndSetIfChanged(ref _notes, value);
    }

    public bool IsGeocoded => Latitude.HasValue && Longitude.HasValue;
}
