using System.Drawing;
using GameHub.Core.Icons;
using Xunit;

namespace GameHub.Core.Tests;

public class IconExtractorTests
{
    [Fact]
    public void TryExtractIcon_ProducesHighResolutionPng_ForRealExecutable()
    {
        var exe = Path.Combine(Environment.SystemDirectory, "mspaint.exe");
        if (!File.Exists(exe)) exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        var output = Path.Combine(Path.GetTempPath(), "GameHubIconTest_" + Guid.NewGuid() + ".png");
        try
        {
            Assert.True(IconExtractor.TryExtractIcon(exe, output));

            using var image = Image.FromFile(output);
            Assert.True(image.Width >= 64, $"expected a large icon but got {image.Width}x{image.Height}");
        }
        finally
        {
            try { File.Delete(output); } catch { }
        }
    }

    [Fact]
    public void TryExtractIcon_ReturnsFalse_ForMissingFile()
    {
        var output = Path.Combine(Path.GetTempPath(), "GameHubIconTest_" + Guid.NewGuid() + ".png");
        Assert.False(IconExtractor.TryExtractIcon(@"C:\definitely\missing\game.exe", output));
    }
}
