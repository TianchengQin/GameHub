using System.Drawing;
using System.Drawing.Imaging;

namespace GameHub.Core.Icons;

public static class IconExtractor
{
    /// <summary>
    /// Extracts the icon associated with an executable and saves it as a PNG.
    /// Returns false (without throwing) if the exe has no icon or can't be read.
    /// </summary>
    public static bool TryExtractIcon(string exePath, string outputPngPath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null) return false;

            var directory = Path.GetDirectoryName(outputPngPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var bitmap = icon.ToBitmap();
            bitmap.Save(outputPngPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
