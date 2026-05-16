namespace FB.BackendAPI.Options;

public sealed class FacebookGraphOptions
{
    public const string SectionName = "FacebookGraph";

    public string AppId { get; set; } = string.Empty;

    public string DefaultPageId { get; set; } = string.Empty;

    public string GraphVersion { get; set; } = "v22.0";

    public string PageAccessToken { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}
