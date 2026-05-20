using FB.CoreService.Models;
using FB.CoreService.Options;
using FB.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace FB.CoreService.Services;

public sealed class AutomationRuleEngine : IAutomationRuleEngine
{
    private readonly AutomationOptions _options;

    public AutomationRuleEngine(IOptions<AutomationOptions> options)
    {
        _options = options.Value;
    }

    public AutomationDecision Evaluate(RawEvent rawEvent, AiClassificationResult classification, SpamDetectionResult spamResult, UserActivitySnapshot activity)
    {
        if (spamResult.IsMalicious)
        {
            return new AutomationDecision(
                ProcessingState.PendingReview,
                "Malicious link detected, hidden immediately and pending manual review.",
                activity.IsBlacklisted,
                [BuildHideCommand(rawEvent, classification, "malicious_link_review", requiresManualReview: true)]);
        }

        if (spamResult.IsSpam)
        {
            return new AutomationDecision(
                activity.IsBlacklisted ? ProcessingState.PendingReview : ProcessingState.Processed,
                activity.IsBlacklisted
                    ? "Blacklisted spam detected, hidden immediately and kept for review."
                    : "Light spam detected, hidden immediately.",
                activity.IsBlacklisted,
                [BuildHideCommand(rawEvent, classification, "light_spam", requiresManualReview: false)]);
        }

        if (activity.EventsLastMinute >= _options.RateLimitPerMinute)
        {
            return new AutomationDecision(ProcessingState.PendingReview, "Rate limit threshold reached, skipped AI automation.", activity.IsBlacklisted, []);
        }

        if (activity.IsBlacklisted)
        {
            return new AutomationDecision(ProcessingState.PendingReview, "User is blacklisted because of repeated spam.", true, []);
        }

        if (classification.Intent == "ask_price" && !string.IsNullOrWhiteSpace(rawEvent.CommentId))
        {
            return new AutomationDecision(
                ProcessingState.Processed,
                "Price inquiry auto-reply queued.",
                false,
                [BuildReplyCommand(rawEvent, classification, "Shop đã gửi thông tin chi tiết qua inbox.")]);
        }

        if (classification.Sentiment == "negative" && !string.IsNullOrWhiteSpace(rawEvent.CommentId))
        {
            return new AutomationDecision(
                ProcessingState.Processed,
                "Negative sentiment detected, apology auto-reply queued.",
                false,
                [BuildReplyCommand(rawEvent, classification, "Rất xin lỗi vì trải nghiệm chưa tốt, bên mình sẽ kiểm tra ngay.")]);
        }

        if (classification.Sentiment == "positive" && !string.IsNullOrWhiteSpace(rawEvent.CommentId))
        {
            return new AutomationDecision(
                ProcessingState.Processed,
                "Positive sentiment detected, thank-you auto-reply queued.",
                false,
                [BuildReplyCommand(rawEvent, classification, "Cảm ơn bạn đã ủng hộ shop!")]);
        }

        if (classification.Intent == "support_request" && !string.IsNullOrWhiteSpace(rawEvent.CommentId))
        {
            return new AutomationDecision(
                ProcessingState.Processed,
                "Support request detected, apology auto-reply queued.",
                false,
                [BuildReplyCommand(rawEvent, classification, "Rất xin lỗi vì trải nghiệm chưa tốt, bên mình sẽ kiểm tra ngay.")]);
        }

        return new AutomationDecision(ProcessingState.Processed, "Event processed without automation.", false, []);
    }

    private static ReplyCommand BuildHideCommand(RawEvent rawEvent, AiClassificationResult classification, string reason, bool requiresManualReview)
    {
        return new ReplyCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            EventId = rawEvent.EventId,
            Action = "hide_comment",
            Target = new ReplyTarget
            {
                PageId = rawEvent.PageId,
                CommentId = rawEvent.CommentId,
                PostId = rawEvent.PostId
            },
            Intent = classification.Intent,
            Sentiment = classification.Sentiment,
            Reason = reason,
            RequiresManualReview = requiresManualReview
        };
    }

    private static ReplyCommand BuildReplyCommand(RawEvent rawEvent, AiClassificationResult classification, string replyText)
    {
        return new ReplyCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            EventId = rawEvent.EventId,
            Action = "reply",
            Target = new ReplyTarget
            {
                PageId = rawEvent.PageId,
                CommentId = rawEvent.CommentId,
                PostId = rawEvent.PostId
            },
            ReplyText = replyText,
            Intent = classification.Intent,
            Sentiment = classification.Sentiment,
            Reason = "auto_reply"
        };
    }
}
