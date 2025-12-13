using System.Collections.ObjectModel;
using System.ComponentModel;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Services;

public class EvaluationService(IPointOfInterestRepository poiRepo) : IEvaluationService
{    
    private readonly IPointOfInterestRepository _poiRepo = poiRepo;

    public async Task<EvaluationResult> ScoreAsync(Location location, CriteriaNode node, CancellationToken ct = default)
    {
        return node switch
        {
            GroupNode g => await ProcessGroupNode(location, g, ct),
            TermNode t => await ProcessTermNode(location, t, ct),
            _ => throw new InvalidEnumArgumentException("Unsupported node type")
        };
    }

    private async Task<EvaluationResult> ProcessGroupNode(Location location, GroupNode node, CancellationToken ct = default)
    {
        var children = new ObservableCollection<EvaluationResult>(await Task.WhenAll(node.Children.Select(c => ScoreAsync(location, c, ct))));
        var score = node.Operator switch
        {
            OperatorType.And => children.Max(v => v.Score),
            OperatorType.Or => children.Min(v => v.Score),
            _ => throw new InvalidEnumArgumentException("Unsupported operator type")
        };
        return new EvaluationResult(node, score, children);
    }

    private async Task<EvaluationResult> ProcessTermNode(Location location, TermNode node, CancellationToken ct = default)
    {
        var bbox = GetBoundingBoxByLocationAndRadius(location, node.DistAmt);
        var poiList = await _poiRepo.ListByBoundingBoxAndCategoryAsync(bbox, (Category)node.CategoryId);
        var closest = poiList
            .Select(poi => GetDistanceInKm(location, poi))
            .DefaultIfEmpty(node.DistAmt + 1)
            .Min();
        var score = Normalize(node.DistAmt, closest);
        return new EvaluationResult(node, score, new ObservableCollection<EvaluationResult>());
    }
    
    const double EarthRadiusKm = 6371.0;
    private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;
    private static double RadiansToDegrees(double rad) => rad * 180.0 / Math.PI;
    
    private BoundingBox GetBoundingBoxByLocationAndRadius(Location location, double distance)
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

    private double GetDistanceInKm(Location location, PointOfInterest poi)
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
        return 1 - (value / maxValue);
    }
}