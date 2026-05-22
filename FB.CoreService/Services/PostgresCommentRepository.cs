using FB.Shared.Contracts;
using FB.Shared.Database;
using Npgsql;

namespace FB.CoreService.Services;

public sealed class PostgresCommentRepository : ICommentRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public PostgresCommentRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task UpsertReceivedAsync(RawEvent rawEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawEvent.CommentId) || string.IsNullOrWhiteSpace(rawEvent.PostId))
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            insert into comments (comment_id, post_id, message, status)
            values (@commentId, @postId, @message, 'received')
            on conflict (comment_id)
            do update set
                post_id = excluded.post_id,
                message = excluded.message,
                status = excluded.status;
            """,
            connection);
        command.Parameters.AddWithValue("commentId", rawEvent.CommentId);
        command.Parameters.AddWithValue("postId", rawEvent.PostId);
        command.Parameters.AddWithValue("message", (object?)rawEvent.Message ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAnalysisAsync(string? commentId, string? intent, string? sentiment, string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commentId))
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            update comments
            set intent = @intent,
                sentiment = @sentiment,
                status = @status
            where comment_id = @commentId;
            """,
            connection);
        command.Parameters.AddWithValue("intent", (object?)intent ?? DBNull.Value);
        command.Parameters.AddWithValue("sentiment", (object?)sentiment ?? DBNull.Value);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("commentId", commentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
