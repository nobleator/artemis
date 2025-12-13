using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IPointOfInterestRepository
{
    Task BulkInsertAsync(IEnumerable<PointOfInterest> poiList, CancellationToken ct = default);
    Task<IEnumerable<PointOfInterest>> ListByBoundingBoxAndCategoryAsync(BoundingBox bbox, Category cat, CancellationToken ct = default);
}
