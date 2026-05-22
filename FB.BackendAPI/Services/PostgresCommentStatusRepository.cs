using FB.Shared.Database;
using Npgsql;

namespace FB.BackendAPI.Services;

public sealed class PostgresCommentStatusRepository : ICommentStatusRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public PostgresCommentStatusRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task UpdateStatusAsync(string? commentId, string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commentId))
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "update comments set status = @status where comment_id = @commentId;",
            connection);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("commentId", commentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
