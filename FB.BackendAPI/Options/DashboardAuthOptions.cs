namespace FB.BackendAPI.Options;

public sealed class DashboardAuthOptions
{
    public const string SectionName = "DashboardAuth";

    public string AdminToken { get; set; } = string.Empty;

    public string HeaderName { get; set; } = "X-Admin-Token";
}
