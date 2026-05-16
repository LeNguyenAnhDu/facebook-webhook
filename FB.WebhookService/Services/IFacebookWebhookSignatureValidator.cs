namespace FB.WebhookService.Services;

public interface IFacebookWebhookSignatureValidator
{
    bool IsValid(string payload, string? signatureHeader);
}
