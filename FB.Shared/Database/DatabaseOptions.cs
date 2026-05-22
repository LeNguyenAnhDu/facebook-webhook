namespace FB.Shared.Database;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=fb_api_db;Username=fb_api_user;Password=fb_api_password";
}
