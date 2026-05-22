using FB.Shared.Database;
using Npgsql;

namespace FB.BackendAPI.Services;

public sealed class PostgresCommandIdempotencyStore : ICommandIdempotencyStore
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public PostgresCommandIdempotencyStore(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> HasProcessedAsync(string commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "select exists(select 1 from idempotency_keys where command_id = @commandId and status = 'processed');",
            connection);
        command.Parameters.AddWithValue("commandId", commandId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task MarkProcessedAsync(string commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            insert into idempotency_keys (command_id, processed_at, status)
            values (@commandId, current_timestamp, 'processed')
            on conflict (command_id)
            do update set
                processed_at = excluded.processed_at,
                status = excluded.status;
            """,
            connection);
        command.Parameters.AddWithValue("commandId", commandId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
