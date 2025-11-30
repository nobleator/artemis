using System.Data;
using Dapper;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Infra.Repositories;

public class CriteriaRepository(IDbConnection db) : ICriteriaRepository
{
    readonly IDbConnection _db = db;

    public async Task<Criteria> AddAsync(Criteria criteria, CancellationToken ct = default)
    {
        var insert = @"
            insert into criteria (lft, rgt, node_id)
            values (@Left, @Right, @NodeId);
            select last_insert_rowid();";
        var select = @"
            select id, lft as Left, rgt as Right, operator, category_id as CategoryId, dist_amt as DistAmt
            from criteria
            where id = @id;";
        var id = await _db.ExecuteScalarAsync<int>(insert, criteria);
        return await _db.QuerySingleAsync<Criteria>(select, new { id });
    }

    public async Task<int> DeleteAsync(int id, CancellationToken ct = default)
    {
        return await _db.ExecuteAsync("delete from criteria where id = @id", new { id });
    }

    public async Task<Criteria?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var select = @"
            select id, lft as Left, rgt as Right, operator, category_id as CategoryId, dist_amt as DistAmt
            from criteria
            where id = @id;";
        return await _db.QuerySingleOrDefaultAsync<Criteria>(select, new { id });
    }

    public async Task<IEnumerable<Criteria>> ListAsync(CancellationToken ct = default)
    {
        var select = @"
            select id, lft as Left, rgt as Right, operator, category_id as CategoryId, dist_amt as DistAmt
            from criteria
            order by lft;
            ";
        return await _db.QueryAsync<Criteria>(select);
    }

    public async Task<Criteria> UpdateAsync(Criteria criteria, CancellationToken ct = default)
    {
        var update = @"
            update criteria
            set lft = @Left,
                rgt = @Right,
                node_id = @NodeId
            where id = @Id;";
        var select = @"
            select id, lft as Left, rgt as Right, node_id as NodeId
            from criteria
            where id = @Id;";
        var rowsChanged = await _db.ExecuteAsync(update, criteria);
        if (rowsChanged != 1)
            Console.WriteLine($"Uh oh, {rowsChanged} changes");
        return await _db.QuerySingleAsync<Criteria>(select, criteria);
    }
}
