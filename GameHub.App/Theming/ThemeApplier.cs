using GameHub.Core.Settings;
using Wpf.Ui.Appearance;

namespace GameHub.App.Theming;

public static class ThemeApplier
{
    public static void Apply(AppTheme theme)
    {
        var resolved = theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.Dark => ApplicationTheme.Dark,
            _ => ApplicationThemeManager.GetSystemTheme() switch
            {
                SystemTheme.Light => ApplicationTheme.Light,
                _ => ApplicationTheme.Dark
            }
        };

        ApplicationThemeManager.Apply(resolved);
    }
}
