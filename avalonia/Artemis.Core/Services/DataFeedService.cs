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

    private IDictionary<Region, BoundingBox> RegionMap = new Dictionary<Region,BoundingBox>{
        { Region.NewYork, new BoundingBox(40.696951, -74.022437, 40.758613, -73.952075) }
    };

    public async Task LoadOverpassPOI(CancellationToken ct = default)
    {
        var poiList = new List<PointOfInterest>();
        var client = _httpClientFactory.CreateClient();
        // client.BaseAddress = new Uri("https://www.overpass-api.de/api/interpreter");
        client.BaseAddress = new Uri("https://www.overpass-api.de");
        var batch = new Batch
        { 
            Id = -1,
            Source = "Overpass",
            RunAt = DateTime.UtcNow
        };
        batch = await _batchRepo.AddAsync(batch, ct);
        foreach (var kvp in RegionMap)
        {
            foreach (Category cat in Enum.GetValues(typeof(Category)))
            {
                var bbox = kvp.Value;
                var filter = GetQuery(cat);
                var query = $"[out:json];nwr{filter}({bbox.MinLat}, {bbox.MinLon}, {bbox.MaxLat}, {bbox.MaxLon});out center;";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("data", query)
                });
                var resp = await client.PostAsync("api/interpreter", content);
                resp.EnsureSuccessStatusCode();
                var data = await resp.Content.ReadFromJsonAsync<OverpassResponse>();
                if (data != null)
                {
                    foreach (var d in data.Elements)
                    {
                        if (d.Center != null)
                        {
                            poiList.Add(new PointOfInterest(-1, d.Id.ToString(), cat, d.Center.Lat, d.Center.Lon));
                        }
                    }
                }
                Thread.Sleep(1500);
            }
        }
        await _poiRepo.BulkInsertAsync(poiList, ct);
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