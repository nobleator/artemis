namespace Artemis.Core.Models;

public record Batch
{
    public required int Id { get; set; }
    public required string Source { get; set; }
    public required string Status { get; set; }
    public required DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public TimeSpan? Elapsed => End - Start;
    public long? RowCount { get; set; }
    public long? CategoryCount { get; set; }
}
