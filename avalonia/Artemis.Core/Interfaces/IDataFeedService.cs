using Artemis.Core.Models;

namespace Artemis.Core.Interfaces;

public interface IDataFeedService
{
    Task LoadOverpassPOI(CancellationToken ct = default);
}
