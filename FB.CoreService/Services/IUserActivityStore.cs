using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public interface IUserActivityStore
{
    UserActivitySnapshot Track(RawEvent rawEvent);
}

public sealed record UserActivitySnapshot(
    int EventsLastMinute,
    int SameMessageCount24Hours,
    bool IsBlacklisted);
