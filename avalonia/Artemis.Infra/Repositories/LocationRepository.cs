using System.Data;
using Dapper;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Infra.Repositories;

public class LocationRepository(IDbConnection db) : ILocationRepository
{
    readonly IDbConnection _db = db;

    public async Task<Location> AddAsync(Location location, CancellationToken ct = default)
    {
        var insert = @"
            insert into location ([name], [address], lat, lon, notes)
            values (@Name, @Address, @Latitude, @Longitude, @Notes);
            select last_insert_rowid();";
        var select = @"
            select
                id,
                [name],
                [address],
                lat as latitude,
                lon as longitude,
                notes
            from location
            where id = @id;";
        var id = await _db.ExecuteScalarAsync<int>(insert, location);
        return await _db.QuerySingleAsync<Location>(select, new { id });
    }

    public async Task<int> DeleteAsync(int id, CancellationToken ct = default)
    {
        return await _db.ExecuteAsync("delete from location where id = @id", new { id });
    }

    public async Task<Location?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var select = @"
            select
                id,
                [name],
                [address],
                lat as latitude,
                lon as longitude,
                notes
            from location
            where id = @id;";
        return await _db.QuerySingleAsync<Location>(select, new { id });
    }

    public async Task<IEnumerable<Location>> ListAsync(CancellationToken ct = default)
    {
        return await _db.QueryAsync<Location>(@"
            select
                id,
                [name],
                [address],
                lat as latitude,
                lon as longitude,
                notes
            from location");
    }

    public async Task<Location> UpdateAsync(Location location, CancellationToken ct = default)
    {
        var update = @"
            update location
            set [name] = @Name,
                [address] = @Address,
                lat = @Latitude,
                lon = @Longitude,
                notes = @Notes
            where id = @Id;";
        var select = @"
            select
                id,
                [name],
                [address],
                lat as latitude,
                lon as longitude,
                notes
            from location
            where id = @Id;";
        var rowsChanged = await _db.ExecuteAsync(update, location);
        if (rowsChanged != 1)
            Console.WriteLine($"Uh oh, {rowsChanged} changes");
        return await _db.QuerySingleAsync<Location>(select, location);
    }
}
