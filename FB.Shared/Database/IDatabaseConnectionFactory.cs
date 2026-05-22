using Npgsql;

namespace FB.Shared.Database;

public interface IDatabaseConnectionFactory
{
    Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
