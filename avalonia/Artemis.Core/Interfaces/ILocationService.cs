using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface ILocationService
{
    Task<Location> GeocodeAsync(Location location, CancellationToken ct = default);
}
