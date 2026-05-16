using System.Collections.Concurrent;

namespace FB.BackendAPI.Services;

public sealed class InMemoryCommandIdempotencyStore : ICommandIdempotencyStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _processedCommands = new();

    public Task<bool> HasProcessedAsync(string commandId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_processedCommands.ContainsKey(commandId));
    }

    public Task MarkProcessedAsync(string commandId, CancellationToken cancellationToken = default)
    {
        _processedCommands[commandId] = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}
