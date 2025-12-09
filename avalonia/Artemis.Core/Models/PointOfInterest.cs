namespace Artemis.Core.Models;

public record PointOfInterest(int Id, string SourceXref, Category Category, double Latitude, double Longitude);
