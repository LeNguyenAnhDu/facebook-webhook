using FB.CoreService.Models;

namespace FB.CoreService.Services;

public interface IEventProcessingStatusStore
{
    bool MarkReceived(string eventId);

    void Upsert(EventStatusSnapshot snapshot);

    bool TryGet(string eventId, out EventStatusSnapshot snapshot);
}
