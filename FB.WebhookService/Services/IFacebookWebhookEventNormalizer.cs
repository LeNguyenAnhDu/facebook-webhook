using FB.Shared.Contracts;

namespace FB.WebhookService.Services;

public interface IFacebookWebhookEventNormalizer
{
    IReadOnlyList<RawEvent> Normalize(string payload);
}
