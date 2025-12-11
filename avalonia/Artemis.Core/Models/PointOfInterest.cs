namespace Artemis.Core.Models;

public record PointOfInterest(int Id, int BatchId, string SourceXref, Category Category, double Latitude, double Longitude);
