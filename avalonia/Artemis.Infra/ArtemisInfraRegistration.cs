using System.Data;
using Artemis.Core.Interfaces;
using Artemis.Core.Services;
using Artemis.Infra.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Artemis.Infra;

public static class ArtemisInfraRegistration
{
    public static IServiceCollection AddArtemisInfra(this IServiceCollection services, string conn)
    {
        services.AddScoped<IDbConnection>(_ => new SqliteConnection(conn));
        services.AddScoped<ICriteriaRepository, CriteriaRepository>();
        services.AddScoped<ICriteriaTreeService, CriteriaTreeService>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IPointOfInterestRepository, PointOfInterestRepository>();
        services.AddScoped<IScoreRepository, ScoreRepository>();
        services.AddHttpClient();
        services.AddScoped<IDataFeedService, DataFeedService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<ILocationService, LocationService>();
        return services;
    }
}
