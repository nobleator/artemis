using System.Net.Http.Json;
using System.Text.Json;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Services;

public class DataFeedService(IHttpClientFactory httpClientFactory, IBatchRepository batchRepo, IPointOfInterestRepository poiRepo) : IDataFeedService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IBatchRepository _batchRepo = batchRepo;
    private readonly IPointOfInterestRepository _poiRepo = poiRepo;

    private readonly IDictionary<Region, BoundingBox> RegionMap = new Dictionary<Region,BoundingBox>{
        { Region.NewYork, new BoundingBox(40.696951, -74.022437, 40.758613, -73.952075) }
    };

    public async Task<IEnumerable<Batch>> ListBatchesAsync(CancellationToken ct = default)
    {
        return await _batchRepo.ListAsync(ct);
    }

    public async Task LoadOverpassPOI(IProgress<double>? progress = null,CancellationToken ct = default)
    {
        Console.WriteLine("Starting Overpass data load...");
        var total = RegionMap.Count * Enum.GetValues<Category>().Length + 2; // Small padding to account for final load to database
        var completed = 0;
        progress?.Report(0);
        var poiList = new List<PointOfInterest>();
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://www.overpass-api.de");
        var batch = new Batch
        { 
            Id = -1,
            Source = "Overpass",
            Status = "Pending",
            Start = DateTime.UtcNow
        };
        batch = await _batchRepo.AddAsync(batch, ct);
        Console.WriteLine($"Processing batch {batch.Id}...");
        try
        {
            foreach (var kvp in RegionMap)
            {
                Console.WriteLine($"Processing region {kvp.Key}...");
                foreach (Category cat in Enum.GetValues<Category>())
                {
                    Console.WriteLine($"Processing category {cat}...");
                    var bbox = kvp.Value;
                    var filter = GetQuery(cat);
                    var query = $"[out:json];nwr{filter}({bbox.MinLat}, {bbox.MinLon}, {bbox.MaxLat}, {bbox.MaxLon});out center;";
                    var content = new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>("data", query)
                    ]);
                    var resp = await client.PostAsync("api/interpreter", content, ct);
                    resp.EnsureSuccessStatusCode();
                    var data = await resp.Content.ReadFromJsonAsync<OverpassResponse>(ct);
                    if (data != null)
                    {
                        foreach (var d in data.Elements)
                        {
                            if (d.Center != null)
                            {
                                poiList.Add(new PointOfInterest(-1, batch.Id, d.Id.ToString(), cat, d.Center.Lat, d.Center.Lon));
                            }
                        }
                    }
                    completed++;
                    progress?.Report(completed * 100 / total);
                    await Task.Delay(2000, ct);
                }
            }
            await _poiRepo.BulkInsertAsync(poiList, ct);
            completed++;
            progress?.Report(completed * 100 / total);
            batch.Status = "Success";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Batch {batch.Id} failed due to the following exception: {ex.Message}");
            batch.Status = "Failed";
        }
        batch.End = DateTime.UtcNow;
        await _batchRepo.UpdateAsync(batch, ct);
        progress?.Report(100);
        Console.WriteLine($"Overpass data load for batch {batch.Id} complete.");
    }

    private string GetQuery(Category cat)
    {
        return cat switch
        {
            Category.Airport => "[aeroway=terminal]",
            Category.BusStation => "[building][amenity=bus_station]",
            Category.CoffeeShop => "[building][amenity=cafe][cuisine=coffee_shop]",
            Category.Library => "[building][amenity=library]",
            Category.School => "[building][amenity=school]",
            Category.Park => "[leisure=park]",
            Category.Grocery => "[building][shop=supermarket]",
            Category.TrainStation => "[building][building=train_station]",
            Category.PoliceStation => "[building][amenity=police]",
            Category.FireStation => "[building][amenity=fire_station]",
            _ => throw new ArgumentOutOfRangeException(nameof(cat), $"Unexpected category value: {cat}"),
        };
    }
}