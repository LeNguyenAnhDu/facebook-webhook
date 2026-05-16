namespace FB.WebhookService.Options;

public sealed class FacebookWebhookOptions
{
    public const string SectionName = "FacebookWebhook";

    public string VerifyToken { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;
}
