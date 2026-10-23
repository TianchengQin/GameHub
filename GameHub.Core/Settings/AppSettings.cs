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

    public bool MinimizeToTrayOnLaunch { get; set; } = true;

    public bool ShowFloatingFavoritesWindow { get; set; }
    public double? FloatingWindowLeft { get; set; }
    public double? FloatingWindowTop { get; set; }

    public string? SteamGridDbApiKey { get; set; }
}
