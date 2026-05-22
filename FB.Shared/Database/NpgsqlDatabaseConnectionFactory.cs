using Microsoft.Extensions.Options;
using Npgsql;

namespace FB.Shared.Database;

public sealed class NpgsqlDatabaseConnectionFactory : IDatabaseConnectionFactory
{
    private readonly DatabaseOptions _options;

    public NpgsqlDatabaseConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
