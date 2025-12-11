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

    public async Task LoadOverpassPOI(CancellationToken ct = default)
    {
        Console.WriteLine("Starting Overpass data load...");
        var poiList = new List<PointOfInterest>();
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://www.overpass-api.de");
        var batch = new Batch
        { 
            Id = -1,
            Source = "Overpass",
            Start = DateTime.UtcNow
        };
        batch = await _batchRepo.AddAsync(batch, ct);
        foreach (var kvp in RegionMap)
        {
            foreach (Category cat in Enum.GetValues<Category>())
            {
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
                Thread.Sleep(1500);
            }
        }
        await _poiRepo.BulkInsertAsync(poiList, ct);
        batch.End = DateTime.UtcNow;
        await _batchRepo.UpdateAsync(batch, ct);
        Console.WriteLine("Overpass data load complete.");
    }

    private string GetQuery(Category cat)
    {
        return cat switch
        {
            Category.Airport => "[aeroway=terminal]",
            // Category.Library => "[building][amenity=library]",
            // Category.School => "[building][amenity=school]",
            // Category.Park => "[leisure=park]",
            // Category.Grocery => "[building][shop=supermarket]",
            // Category.CoffeeShop => "[building][amenity=cafe][cuisine=coffee_shop]",
            // Category.TrainStation => "[building][building=train_station]",
            // Category.BusStation => "[building][amenity=bus_station]",
            // Category.PoliceStation => "[building][amenity=police]",
            // Category.FireStation => "[building][amenity=fire_station]",
            _ => throw new ArgumentOutOfRangeException(nameof(cat), $"Unexpected category value: {cat}"),
        };
    }
}