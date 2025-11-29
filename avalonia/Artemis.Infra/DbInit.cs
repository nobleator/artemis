using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Artemis.Infra;

public static class DbInit
{
    public static async Task EnsureCreatedAsync(IServiceProvider sp)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Scripts", "init.sql");
        var sql = await File.ReadAllTextAsync(path);
        using var conn = sp.GetRequiredService<IDbConnection>();
        await conn.ExecuteAsync(sql);
    }
}
