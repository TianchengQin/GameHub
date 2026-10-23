using GameHub.Core.Scanning;
using Xunit;

namespace GameHub.Core.Tests;

public class ShortcutScanTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "GameHubShortcutTests_" + Guid.NewGuid());

    public ShortcutScanTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string MakeExe(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[10]);
        return path;
    }

    [Fact]
    public void ScanShortcuts_KeepsGameExe_AndDropsNoiseAndDuplicates()
    {
        var game = MakeExe("cool_game.exe");
        var uninstall = MakeExe("unins000.exe");

        var map = new Dictionary<string, string?>
        {
            ["Cool Game.lnk"] = game,
            ["Cool Game (copy).lnk"] = game,
            ["Uninstall Cool Game.lnk"] = uninstall,
            ["Cool Game Readme.lnk"] = game,
            ["Website.lnk"] = null,
            ["Document.lnk"] = Path.Combine(_dir, "notes.txt")
        };

        var results = new GameScanner().ScanShortcuts(map.Keys, k => map[k]);

        var single = Assert.Single(results);
        Assert.Equal("Cool Game", single.SuggestedName);
        Assert.Equal(game, single.BestExePath);
    }

    [Fact]
    public void ScanShortcuts_SkipsMissingTargets()
    {
        var results = new GameScanner().ScanShortcuts(
            new[] { "Ghost.lnk" }, _ => Path.Combine(_dir, "gone.exe"));

        Assert.Empty(results);
    }
}
