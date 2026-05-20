using System.Collections.Concurrent;
using FB.CoreService.Models;

namespace FB.CoreService.Services;

public sealed class InMemoryEventProcessingStatusStore : IEventProcessingStatusStore
{
    private readonly ConcurrentDictionary<string, EventStatusSnapshot> _snapshots = new();
    private readonly ConcurrentDictionary<string, byte> _dedup = new();

    public bool MarkReceived(string eventId)
    {
        return _dedup.TryAdd(eventId, 1);
    }

    public void Upsert(EventStatusSnapshot snapshot)
    {
        _snapshots[snapshot.EventId] = snapshot;
    }

    public bool TryGet(string eventId, out EventStatusSnapshot snapshot)
    {
        return _snapshots.TryGetValue(eventId, out snapshot!);
    }
}
