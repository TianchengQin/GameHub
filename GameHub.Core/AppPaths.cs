namespace GameHub.Core;

public static class AppPaths
{
    /// <summary>Set to relocate all app data (used for isolated test runs); defaults to %LocalAppData%\GameHub.</summary>
    public const string DataDirEnvVar = "GAMEHUB_DATA_DIR";

    public static string AppDataRoot
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable(DataDirEnvVar);
            return !string.IsNullOrWhiteSpace(overridePath)
                ? overridePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameHub");
        }
    }

    public static string DatabasePath => Path.Combine(AppDataRoot, "library.db");
    public static string IconsFolder => Path.Combine(AppDataRoot, "icons");
    public static string CoversFolder => Path.Combine(AppDataRoot, "covers");
    public static string LogsFolder => Path.Combine(AppDataRoot, "logs");
    public static string SettingsPath => Path.Combine(AppDataRoot, "settings.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(IconsFolder);
        Directory.CreateDirectory(CoversFolder);
        Directory.CreateDirectory(LogsFolder);
    }
}
