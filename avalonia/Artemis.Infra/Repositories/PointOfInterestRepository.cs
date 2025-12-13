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

    public async Task<IEnumerable<PointOfInterest>> ListByBoundingBoxAndCategoryAsync(BoundingBox bbox, Category cat, CancellationToken ct = default)
    {
        var select = @"
            select
                batch_id as BatchId,
                source_xref as SourceXref,
                category_id as Category,
                lat as Latitude,
                lon as Longitude
            from poi
            where lat between @MinLat and @MaxLat
                and lon between @MinLon and @MaxLon
                and category_id = @Category;";
        return await _db.QueryAsync<PointOfInterest>(select,
            new {
                bbox.MinLat,
                bbox.MaxLat,
                bbox.MinLon,
                bbox.MaxLon,
                Category = cat
            });
    }
}
