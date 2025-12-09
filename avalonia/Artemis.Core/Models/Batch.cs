namespace Artemis.Core.Models;

public record Batch
{
    public required int Id { get; set; }
    public required string Source { get; set; }
    public required DateTime RunAt { get; set; }
}
