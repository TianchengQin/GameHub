using System.Globalization;
using System.Windows.Data;

namespace GameHub.App.Converters;

public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
            return "从未启动";

        var span = DateTime.Now - dateTime;

        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span < TimeSpan.FromMinutes(1)) return "刚刚";
        if (span < TimeSpan.FromHours(1)) return $"{(int)span.TotalMinutes} 分钟前";
        if (span < TimeSpan.FromDays(1)) return $"{(int)span.TotalHours} 小时前";
        if (span < TimeSpan.FromDays(30)) return $"{(int)span.TotalDays} 天前";
        if (span < TimeSpan.FromDays(365)) return $"{(int)(span.TotalDays / 30)} 个月前";
        return $"{(int)(span.TotalDays / 365)} 年前";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
