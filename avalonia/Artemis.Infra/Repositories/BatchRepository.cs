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
            insert into batch (source, start_utc)
            values (@Source, @Start);
            select last_insert_rowid();";
        var select = @"
            select
                id,
                source,
                start_utc as Start
            from batch
            where id = @id;";
        var id = await _db.ExecuteScalarAsync<int>(insert, batch);
        return await _db.QuerySingleAsync<Batch>(select, new { id });
    }

    public async Task<Batch> UpdateAsync(Batch batch, CancellationToken ct = default)
    {
        var update = @"
            update batch
            set source = @Source,
                start_utc = @Start,
                end_utc = @End
            where id = @Id;";
        var select = @"
            select
                id,
                source,
                start_utc as Start,
                end_utc as End
            from batch
            where id = @Id;";
        var rowsChanged = await _db.ExecuteAsync(update, batch);
        if (rowsChanged != 1)
            Console.WriteLine($"Uh oh, {rowsChanged} changes");
        return await _db.QuerySingleAsync<Batch>(select, batch);
    }

    public async Task<IEnumerable<Batch>> ListAsync(CancellationToken ct = default)
    {
        var select = @"
            with row_agg as (
                select batch_id, count(*) as row_count
                from poi
                group by batch_id
            ),
            cat_agg as (
                select batch_id, category_id, count(distinct category_id) as category_count
                from poi
                group by batch_id, category_id
            )
            select
                b.id,
                b.source,
                b.start_utc as Start,
                b.end_utc as End,
                r.row_count as RowCount,
                c.category_count as CategoryCount
            from batch b
            left join row_agg r on r.batch_id = b.id
            left join cat_agg c on c.batch_id = b.id
            order by b.start_utc desc
            limit 20;";
        return await _db.QueryAsync<Batch>(select);
    }
}
