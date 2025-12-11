using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IDataFeedService
{
    Task<IEnumerable<Batch>> ListBatchesAsync(CancellationToken ct = default);
    Task LoadOverpassPOI(CancellationToken ct = default);
}
