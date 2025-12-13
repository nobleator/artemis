namespace Artemis.Core.Models;

public record PointOfInterest
{
    public required int Id { get; set; }
    public required int BatchId { get; set; }
    public required string SourceXref { get; set; }
    public Category Category { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
