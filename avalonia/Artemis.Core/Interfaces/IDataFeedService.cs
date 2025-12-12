using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IDataFeedService
{
    Task<IEnumerable<Batch>> ListBatchesAsync(CancellationToken ct = default);
    Task LoadOverpassPOI(IProgress<double>? progress = null, CancellationToken ct = default);
}
