namespace Artemis.Core.Models;

public record Criteria
{
    public int Id { get; set; }
    public int Left { get; set; }
    public int Right { get; set; }
    public int NodeId { get; set; }
}
