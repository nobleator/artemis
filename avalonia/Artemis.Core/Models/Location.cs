namespace Artemis.Core.Models;

public record Location
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Notes { get; set; }
}
