using FB.Shared.Contracts;

namespace FB.CoreService.Models;

public sealed record AutomationDecision(
    string State,
    string Reason,
    bool IsBlacklisted,
    IReadOnlyList<ReplyCommand> Commands);
