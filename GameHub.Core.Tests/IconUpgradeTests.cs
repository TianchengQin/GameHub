using System.Drawing;
using System.Drawing.Imaging;
using GameHub.Core.Icons;
using Xunit;

namespace GameHub.Core.Tests;

public class IconUpgradeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "GameHubIconUpgrade_" + Guid.NewGuid());

    public IconUpgradeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string MakePng(string name, int size)
    {
        var path = Path.Combine(_dir, name);
        using var bmp = new Bitmap(size, size);
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    private static string RealExe()
    {
        var exe = Path.Combine(Environment.SystemDirectory, "mspaint.exe");
        return File.Exists(exe) ? exe : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
    }

    [Fact]
    public void TryUpgradeIcon_ReplacesLowResIcon()
    {
        var icon = MakePng("old.png", 32);

        Assert.True(IconExtractor.TryUpgradeIcon(RealExe(), icon));
        Assert.True(IconExtractor.GetImageWidth(icon) > 32);
        Assert.False(File.Exists(icon + ".new"));
    }

    [Fact]
    public void TryUpgradeIcon_LeavesAlreadySharpIconAlone()
    {
        var icon = MakePng("sharp.png", 256);
        var before = File.GetLastWriteTimeUtc(icon);

        Assert.False(IconExtractor.TryUpgradeIcon(RealExe(), icon));
        Assert.Equal(before, File.GetLastWriteTimeUtc(icon));
    }

    [Fact]
    public void TryUpgradeIcon_CreatesMissingIcon()
    {
        var icon = Path.Combine(_dir, "missing.png");

        Assert.True(IconExtractor.TryUpgradeIcon(RealExe(), icon));
        Assert.True(File.Exists(icon));
    }

    [Fact]
    public void TryUpgradeIcon_ReturnsFalse_WhenExeIsGone()
    {
        var icon = MakePng("old2.png", 32);

        Assert.False(IconExtractor.TryUpgradeIcon(Path.Combine(_dir, "gone.exe"), icon));
        Assert.Equal(32, IconExtractor.GetImageWidth(icon));
    }
}
