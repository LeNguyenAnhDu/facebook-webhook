namespace FB.BackendAPI.Services;

public interface ICommandIdempotencyStore
{
    Task<bool> HasProcessedAsync(string commandId, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(string commandId, CancellationToken cancellationToken = default);
}
