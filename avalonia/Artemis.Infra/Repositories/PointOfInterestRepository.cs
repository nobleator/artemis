using System.Data;
using Dapper;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Infra.Repositories;

public class PointOfInterestRepository(IDbConnection db) : IPointOfInterestRepository
{
    readonly IDbConnection _db = db;

    public async Task<Batch> AddAsync(Batch batch, CancellationToken ct = default)
    {
        var insert = @"
            insert into batch (source, run_at)
            values (@Source, @RunAt);
            select last_insert_rowid();";
        var select = @"
            select
                id,
                source,
                run_at as RunAt
            from batch
            where id = @id;";
        var id = await _db.ExecuteScalarAsync<int>(insert, batch);
        return await _db.QuerySingleAsync<Batch>(select, new { id });
    }

    public async Task BulkInsertAsync(IEnumerable<PointOfInterest> poiList, CancellationToken ct = default)
    {
        var insert = @"
            insert into poi (category_id, lat, lon)
            values (@Category, @Latitude, @Longitude)
            where not exists (select 1 from poi where source_xref = @SourceXref);";
        using var conn = _db;
        conn.Open();
        using var tx = _db.BeginTransaction();
        foreach (var poi in poiList)
            await _db.ExecuteAsync(insert, poi, tx);
        tx.Commit();
    }
}
