using System.Data;
using Dapper;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Infra.Repositories;

public class BatchRepository(IDbConnection db) : IBatchRepository
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
}
