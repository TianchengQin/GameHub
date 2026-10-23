using System.Globalization;
using System.Windows.Data;

namespace GameHub.App.Converters;

public class FavoriteGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "★" : "☆";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
