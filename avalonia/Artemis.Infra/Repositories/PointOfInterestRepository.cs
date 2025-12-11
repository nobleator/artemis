using System.Data;
using Dapper;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Infra.Repositories;

public class PointOfInterestRepository(IDbConnection db) : IPointOfInterestRepository
{
    readonly IDbConnection _db = db;

    public async Task BulkInsertAsync(IEnumerable<PointOfInterest> poiList, CancellationToken ct = default)
    {
        var insert = @"
            insert into poi (batch_id, source_xref, category_id, lat, lon)
            select @BatchId, @SourceXref, @Category, @Latitude, @Longitude
            where not exists (
                select 1 from poi where source_xref = @SourceXref
            );";
        using var conn = _db;
        conn.Open();
        using var tx = _db.BeginTransaction();
        foreach (var poi in poiList)
            await _db.ExecuteAsync(insert, poi, tx);
        tx.Commit();
    }
}
