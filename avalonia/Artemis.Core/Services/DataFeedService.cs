using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Services;

public class DataFeedService(IHttpClientFactory httpClientFactory, IBatchRepository batchRepo, IPointOfInterestRepository poiRepo) : IDataFeedService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IBatchRepository _batchRepo = batchRepo;
    private readonly IPointOfInterestRepository _poiRepo = poiRepo;

    private readonly Dictionary<Region, BoundingBox> RegionMap = new()
    {
        { Region.NewYork, new BoundingBox(40.696951, -74.022437, 40.758613, -73.952075) },
        { Region.WashingtonDC, new BoundingBox(38.837447, -77.136211, 38.962575, -76.977940) },
    };

    public async Task<IEnumerable<Batch>> ListBatchesAsync(CancellationToken ct = default)
    {
        return await _batchRepo.ListAsync(ct);
    }

    public async Task LoadOverpassPOI(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.GetTimestamp();
        Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} Starting Overpass data load...");
        var total = RegionMap.Count + 2; // Small padding to account for final load to database
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
        Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} Processing batch {batch.Id}...");
        var first = true;
        try
        {
            foreach (var r in RegionMap)
            {
                if (!first)
                    await Task.Delay(2000, ct);
                first = false;
                var bbox = r.Value;
                var query = "[out:json];(";
                query += string.Join("\n", Enum.GetValues<Category>().Select(c => GetQuery(c, bbox)));
                query += ");out center;";
                var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
                var resp = await client.PostAsync("api/interpreter", content, ct);
                resp.EnsureSuccessStatusCode();
                var data = await resp.Content.ReadFromJsonAsync<OverpassResponse>(ct);
                Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} API returned...");
                if (data != null)
                {
                    Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} {total} elements to process...");
                    foreach (var d in data.Elements)
                    {
                        var cat = Classify(d);
                        if (d.Center != null && cat != null)
                        {
                            poiList.Add(new PointOfInterest
                            {
                                Id = -1,
                                BatchId = batch.Id,
                                SourceXref = d.Id.ToString(),
                                Category = cat.Value,
                                Latitude = d.Center.Lat,
                                Longitude = d.Center.Lon
                            });
                        }
                    }
                    Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} done processing {poiList.Count} elements.");
                }
                completed++;
                progress?.Report(completed * 100 / total);
            }
            Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} inserting {poiList.Count} elements to DB...");
            await _poiRepo.BulkInsertAsync(poiList, ct);
            Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} DB updated.");
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
        Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} Updating batch record with status...");
        await _batchRepo.UpdateAsync(batch, ct);
        progress?.Report(100);
        Console.WriteLine($"{Stopwatch.GetElapsedTime(sw)} Overpass data load for batch {batch.Id} complete.");
    }

    private static string GetQuery(Category cat, BoundingBox bbox)
    {
        var sb = new StringBuilder();
        if (CategorySelectors.TryGetValue(cat, out var selector))
        {
            sb.Append("nwr");
            foreach (var s in selector)
            {
                if (s.Value is null)
                    sb.Append($"[{s.Key}]");
                else
                    sb.Append($"[{s.Key}={s.Value}]");
            }
            sb.Append($"({bbox.MinLat}, {bbox.MinLon}, {bbox.MaxLat}, {bbox.MaxLon});");
        }
        return sb.ToString();
    }

    public sealed record TagSelector(
        string Key,
        string? Value = null
    );

    public static readonly Dictionary<Category, TagSelector[]> CategorySelectors =
        new()
        {
            [Category.Airport]       = [new TagSelector("aeroway", "terminal")],
            [Category.BusStation]    = [new TagSelector("amenity", "bus_station")],
            [Category.CoffeeShop]    = [
                                        new TagSelector("amenity", "cafe"),
                                        new TagSelector("cuisine", "coffee_shop")
                                    ],
            [Category.Library]       = [new TagSelector("amenity", "library")],
            [Category.School]        = [new TagSelector("amenity", "school")],
            [Category.Park]          = [new TagSelector("leisure", "park")],
            [Category.Grocery]       = [new TagSelector("shop", "supermarket")],
            [Category.TrainStation]  = [new TagSelector("building", "train_station")],
            [Category.PoliceStation] = [new TagSelector("amenity", "police")],
            [Category.FireStation]   = [new TagSelector("amenity", "fire_station")]
        };

    private static Category? Classify(Element e)
    {
        if (e.Tags is null)
            return null;

        foreach (var (cat, selectors) in CategorySelectors)
        {
            var match = true;
            foreach (var s in selectors)
            {
                if (!e.Tags.TryGetValue(s.Key, out var val))
                {
                    match = false;
                    break;
                }
                if (s.Value != null && !StringComparer.OrdinalIgnoreCase.Equals(val, s.Value))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return cat;
        }

        return null;
    }
}