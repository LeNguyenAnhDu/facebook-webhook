using System.Collections.Concurrent;

namespace FB.BackendAPI.Services;

public sealed class InMemoryCommandStatusStore : ICommandStatusStore
{
    private readonly ConcurrentDictionary<string, CommandStatusSnapshot> _states = new();

    public void Upsert(CommandStatusSnapshot snapshot)
    {
        _states[snapshot.CommandId] = snapshot;
    }

    public bool TryGet(string commandId, out CommandStatusSnapshot snapshot)
    {
        return _states.TryGetValue(commandId, out snapshot!);
    }
}
