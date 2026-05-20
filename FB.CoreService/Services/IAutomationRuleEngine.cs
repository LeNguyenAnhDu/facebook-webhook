using FB.CoreService.Models;
using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public interface IAutomationRuleEngine
{
    AutomationDecision Evaluate(RawEvent rawEvent, AiClassificationResult classification, SpamDetectionResult spamResult, UserActivitySnapshot activity);
}
