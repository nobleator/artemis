using System.ComponentModel;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Services;

public class EvaluationService(ICriteriaTreeService criteriaService, ILocationRepository locationRepo, IScoreRepository scoreRepo, IPointOfInterestRepository poiRepo) : IEvaluationService
{    
    private readonly ICriteriaTreeService _criteriaService = criteriaService;
    private readonly ILocationRepository _locationRepo = locationRepo;
    private readonly IScoreRepository _scoreRepo = scoreRepo;
    private readonly IPointOfInterestRepository _poiRepo = poiRepo;

    public async Task ScoreAllAsync(CancellationToken ct = default)
    {
        var locations = await _locationRepo.ListAsync(ct);
        var tree = await _criteriaService.GetRoot(ct);
        var scoreAccumulator = new Dictionary<int, EvaluationResult>();
        foreach (var location in locations)
        {
            await ScoreAsync(location, tree, scoreAccumulator, ct);
            var scores = scoreAccumulator
                .Select(kvp => new Score
                {
                    Id = -1,
                    LocationId = location.Id,
                    CriteriaId = kvp.Key,
                    RawValue = kvp.Value.RawScore,
                    NormalizedValue = kvp.Value.Score,
                }).ToList();
            await _scoreRepo.SaveTreeAsync(location.Id, scores, ct);
        }
    }

    public async Task<EvaluationResult> ScoreAsync(Location location, CriteriaNode node, IDictionary<int, EvaluationResult> sink, CancellationToken ct = default)
    {
        var result = node switch
        {
            GroupNode g => await ProcessGroupNode(location, g, sink, ct),
            TermNode t => await ProcessTermNode(location, t, sink, ct),
            _ => throw new InvalidEnumArgumentException("Unsupported node type")
        };
        sink.TryAdd(node.Id, result);
        return result;
    }

    private async Task<EvaluationResult> ProcessGroupNode(Location location, GroupNode node, IDictionary<int, EvaluationResult> sink, CancellationToken ct = default)
    {
        var children = await Task.WhenAll(node.Children.Select(c => ScoreAsync(location, c, sink, ct)));
        var rawScore = node.Operator switch
        {
            OperatorType.And => children.Length != 0 ? children.Max(v => v.RawScore) : 0,
            OperatorType.Or => children.Length != 0 ? children.Min(v => v.RawScore) : 0,
            _ => throw new InvalidEnumArgumentException("Unsupported operator type")
        };
        var score = node.Operator switch
        {
            OperatorType.And => children.Length != 0 ? children.Max(v => v.Score) : 0,
            OperatorType.Or => children.Length != 0 ? children.Min(v => v.Score) : 0,
            _ => throw new InvalidEnumArgumentException("Unsupported operator type")
        };
        var result = new EvaluationResult(node.Id, rawScore, score);
        sink.TryAdd(node.Id, result);
        return result;
    }

    private async Task<EvaluationResult> ProcessTermNode(Location location, TermNode node, IDictionary<int, EvaluationResult> sink, CancellationToken ct = default)
    {
        Console.WriteLine($"Processing node {node.Id} with min {node.DistAmt}...");
        var bbox = GetBoundingBoxByLocationAndRadius(location, node.DistAmt);
        var poiList = await _poiRepo.ListByBoundingBoxAndCategoryAsync(bbox, node.Category, ct);
        Console.WriteLine($"{poiList.Count()} POI matches");
        var closest = poiList
            .Select(poi => GetDistanceInKm(location, poi))
            .DefaultIfEmpty(node.DistAmt + 1)
            .Min();
        Console.WriteLine($"Closest POI: {closest}");
        var score = Normalize(node.DistAmt, closest);
        Console.WriteLine($"Normalized score: {score}");
        var result = new EvaluationResult(node.Id, closest, score);
        sink.TryAdd(node.Id, result);
        Console.WriteLine($"Finished processing node {node.Id}.");
        return result;
    }
    
    const double EarthRadiusKm = 6371.0;
    private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;
    private static double RadiansToDegrees(double rad) => rad * 180.0 / Math.PI;
    
    private static BoundingBox GetBoundingBoxByLocationAndRadius(Location location, double distance)
    {
        if (!location.Latitude.HasValue || !location.Longitude.HasValue)
            throw new Exception($"Uh oh, need to geocode location {location}");
        var latRad = DegreesToRadians(location.Latitude.Value);
        var deltaLat = distance / EarthRadiusKm;
        var deltaLon = distance / (EarthRadiusKm * Math.Cos(latRad));
        return new BoundingBox(
            location.Latitude.Value - RadiansToDegrees(deltaLat),
            location.Longitude.Value - RadiansToDegrees(deltaLon),
            location.Latitude.Value + RadiansToDegrees(deltaLat),
            location.Longitude.Value  + RadiansToDegrees(deltaLon)
        );
    }

    private static double GetDistanceInKm(Location location, PointOfInterest poi)
    {
        if (!location.Latitude.HasValue || !location.Longitude.HasValue)
            throw new Exception($"Uh oh, need to geocode location {location}");
        var dLat = DegreesToRadians(location.Latitude.Value - poi.Latitude);
        var dLon = DegreesToRadians(location.Longitude.Value - poi.Longitude);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(DegreesToRadians(poi.Latitude)) *
            Math.Cos(DegreesToRadians(location.Latitude.Value)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }
    
    private static double Normalize(double maxValue, double value)
    {
        return Math.Max(1 - (value / maxValue), 0);
    }

    public async Task<IEnumerable<Score>> ListAsync(CancellationToken ct = default)
    {
        return await _scoreRepo.ListAsync(ct);
    }
}