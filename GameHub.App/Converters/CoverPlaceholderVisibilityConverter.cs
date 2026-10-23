using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace GameHub.App.Converters;

/// <summary>Shows the placeholder glyph only when there is no valid cover/icon file to display.</summary>
public class CoverPlaceholderVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasCover = value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        return hasCover ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
