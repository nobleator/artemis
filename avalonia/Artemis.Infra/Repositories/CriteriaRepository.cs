using System.Data;
using Dapper;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Infra.Repositories;

public class CriteriaRepository(IDbConnection db) : ICriteriaRepository
{
    readonly IDbConnection _db = db;

    public async Task<IEnumerable<Criteria>> ListAsync(CancellationToken ct = default)
    {
        var select = @"
            select id, lft as Left, rgt as Right, operator, category_id as CategoryId, dist_amt as DistAmt
            from criterion
            order by lft;
            ";
        return await _db.QueryAsync<Criteria>(select);
    }

    public async Task SaveTreeAsync(IEnumerable<Criteria> items, CancellationToken ct = default)
    {
        var insert = @"
            insert into criterion (lft, rgt, operator, category_id, dist_amt)
            values (@Left, @Right, @Operator, @CategoryId, @DistAmt);";
        using var conn = _db;
        conn.Open();
        using var tx = _db.BeginTransaction();
        await _db.ExecuteAsync("delete from criterion;", tx);
        foreach (var c in items)
            await _db.ExecuteAsync(insert, c, tx);
        tx.Commit();
    }
}
