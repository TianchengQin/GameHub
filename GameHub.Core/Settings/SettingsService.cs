using System.Text.Json;

namespace GameHub.Core.Settings;

public class SettingsService : ISettingsService
{
    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        Current = Load();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
            {
                var json = File.ReadAllText(AppPaths.SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Corrupt or unreadable settings file: fall back to defaults rather than crash.
        }

        return new AppSettings();
    }

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataRoot);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            var temp = AppPaths.SettingsPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, AppPaths.SettingsPath, overwrite: true);
            return true;
        }
        catch
        {
            // A failed save shouldn't crash the app; callers that care can check the result.
            return false;
        }
    }
}
