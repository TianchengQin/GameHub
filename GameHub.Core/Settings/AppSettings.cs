using GameHub.Core.Scanning;

namespace GameHub.Core.Settings;

public enum AppTheme
{
    Light,
    Dark,
    System
}

public class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public List<string> BlacklistPatterns { get; set; } = new ScanOptions().BlacklistPatterns;
}
