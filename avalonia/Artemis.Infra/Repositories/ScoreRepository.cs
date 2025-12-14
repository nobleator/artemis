using System.Data;
using Dapper;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Infra.Repositories;

public class ScoreRepository(IDbConnection db) : IScoreRepository
{
    readonly IDbConnection _db = db;
    
    public async Task SaveTreeAsync(int locationId, IEnumerable<Score> items, CancellationToken ct = default)
    {
        if (items.Any(x => x.LocationId != locationId))
            throw new InvalidOperationException("Cannot mix scores with unrelated locations");
        var insert = @"
            insert into score (location_id, criterion_id, raw_value, norm_value)
            values (@LocationId, @CriteriaId, @RawValue, @NormalizedValue);";
        using var conn = _db;
        conn.Open();
        using var tx = _db.BeginTransaction();
        await _db.ExecuteAsync("delete from score where location_id = @locationId;", new { locationId }, tx);
        foreach (var c in items)
            await _db.ExecuteAsync(insert, c, tx);
        tx.Commit();
    }

    public async Task<IEnumerable<Score>> ListAsync(CancellationToken ct = default)
    {
        var select = @"
            select
                id,
                location_id as LocationId,
                criterion_id as CriteriaId,
                raw_value as RawValue,
                norm_value as NormalizedValue
            from score;";
        return await _db.QueryAsync<Score>(select);
    }
}
