namespace Artemis.Core.Models;

public record Criteria
{
    public int Id { get; set; }
    public int Left { get; set; }
    public int Right { get; set; }
    public int? Operator { get; init; }
    public int? CategoryId { get; init; }
    public decimal? DistAmt { get; init; }
}
