using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FB.Shared.Database;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddSingleton<IDatabaseConnectionFactory, NpgsqlDatabaseConnectionFactory>();
        return services;
    }
}
