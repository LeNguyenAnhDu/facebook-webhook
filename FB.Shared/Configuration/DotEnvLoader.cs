using DotNetEnv;

namespace FB.Shared.Configuration;

public static class DotEnvLoader
{
    public static void LoadFromRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDirectory is not null)
        {
            var envPath = Path.Combine(currentDirectory.FullName, ".env");
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                return;
            }

            currentDirectory = currentDirectory.Parent;
        }
    }
}
