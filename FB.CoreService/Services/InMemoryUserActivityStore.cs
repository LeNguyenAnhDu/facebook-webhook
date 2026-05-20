using System.Collections.Concurrent;
using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public sealed class InMemoryUserActivityStore : IUserActivityStore
{
    private readonly ConcurrentDictionary<string, List<TrackedUserEvent>> _events = new();
    private readonly ConcurrentDictionary<string, bool> _blacklist = new();

    public UserActivitySnapshot Track(RawEvent rawEvent)
    {
        var userKey = rawEvent.UserId ?? "anonymous";
        var message = rawEvent.Message?.Trim().ToLowerInvariant() ?? string.Empty;
        var now = DateTimeOffset.UtcNow;

        var list = _events.GetOrAdd(userKey, _ => []);
        lock (list)
        {
            list.RemoveAll(item => item.CreatedAt < now.AddHours(-24));
            list.Add(new TrackedUserEvent(message, now));

            var lastMinute = list.Count(item => item.CreatedAt >= now.AddMinutes(-1));
            var repeated = list.Count(item => item.Message == message);
            var blacklisted = _blacklist.ContainsKey(userKey);

            if (repeated >= 3 && message.Contains("http", StringComparison.OrdinalIgnoreCase))
            {
                _blacklist[userKey] = true;
                blacklisted = true;
            }

            return new UserActivitySnapshot(lastMinute, repeated, blacklisted);
        }
    }

    private sealed record TrackedUserEvent(string Message, DateTimeOffset CreatedAt);
}
