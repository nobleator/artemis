namespace Artemis.Core.Models;

public record Score
{
    public required int Id { get; set; }
    public required int LocationId { get; set; }
    public required int CriteriaId { get; set; }
    public required double RawValue { get; set; }
    public required double NormalizedValue { get; set; }
}
