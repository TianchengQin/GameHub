using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GameHub.Core.Icons;
using Xunit;

namespace GameHub.Core.Tests;

public class IconOrientationTests
{
    private const int Grid = 24;

    // Renders onto white and shrinks to a small grayscale grid so only the overall layout is compared.
    private static double[,] Thumbnail(Image source, bool flipVertically)
    {
        using var canvas = new Bitmap(Grid, Grid, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, new Rectangle(0, 0, Grid, Grid));
        }

        if (flipVertically) canvas.RotateFlip(RotateFlipType.RotateNoneFlipY);

        var result = new double[Grid, Grid];
        for (var y = 0; y < Grid; y++)
        for (var x = 0; x < Grid; x++)
        {
            var c = canvas.GetPixel(x, y);
            result[x, y] = (c.R + c.G + c.B) / 3.0;
        }

        return result;
    }

    private static double Difference(double[,] a, double[,] b)
    {
        var total = 0.0;
        for (var y = 0; y < Grid; y++)
        for (var x = 0; x < Grid; x++)
            total += Math.Abs(a[x, y] - b[x, y]);

        return total / (Grid * Grid);
    }

    [Theory]
    [InlineData("mspaint.exe")]
    [InlineData("notepad.exe")]
    [InlineData("charmap.exe")]
    public void ExtractedIcon_IsUpright(string exeName)
    {
        var exe = Path.Combine(Environment.SystemDirectory, exeName);
        if (!File.Exists(exe)) exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), exeName);
        if (!File.Exists(exe)) return;

        var output = Path.Combine(Path.GetTempPath(), "GameHubOrient_" + Guid.NewGuid() + ".png");
        try
        {
            Assert.True(IconExtractor.TryExtractIcon(exe, output));

            using var extracted = Image.FromFile(output);
            using var reference = Icon.ExtractAssociatedIcon(exe)!.ToBitmap();

            var sensitivity = Difference(Thumbnail(reference, true), Thumbnail(reference, false));
            Assert.True(sensitivity > 3, $"{exeName}'s icon is too vertically symmetric to detect a flip ({sensitivity:F1})");

            var upright = Difference(Thumbnail(extracted, false), Thumbnail(reference, false));
            var flipped = Difference(Thumbnail(extracted, true), Thumbnail(reference, false));

            Assert.True(upright < flipped, $"icon looks vertically flipped (upright diff {upright:F1}, flipped diff {flipped:F1})");
        }
        finally
        {
            try { File.Delete(output); } catch { }
        }
    }
}
