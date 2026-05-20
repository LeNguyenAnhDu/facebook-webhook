using FB.Shared.Contracts;

namespace FB.BackendAPI.Services;

public interface ICommandStatusStore
{
    void Upsert(CommandStatusSnapshot snapshot);

    bool TryGet(string commandId, out CommandStatusSnapshot snapshot);
}

public sealed record CommandStatusSnapshot(
    string CommandId,
    string EventId,
    string State,
    string Action,
    string? Detail,
    DateTimeOffset UpdatedAt);
