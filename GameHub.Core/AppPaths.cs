namespace GameHub.Core;

public static class AppPaths
{
    public static string AppDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameHub");

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
