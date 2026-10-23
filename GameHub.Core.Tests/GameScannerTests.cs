using GameHub.Core.Scanning;
using Xunit;

namespace GameHub.Core.Tests;

public class GameScannerTests : IDisposable
{
    private readonly string _root;

    public GameScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "GameHubTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private string CreateGameFolder(string folderName, params (string relativePath, int sizeBytes)[] exeFiles)
    {
        var folder = Path.Combine(_root, folderName);
        Directory.CreateDirectory(folder);

        foreach (var (relativePath, sizeBytes) in exeFiles)
        {
            var fullPath = Path.Combine(folder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, new byte[sizeBytes]);
        }

        return folder;
    }

    [Theory]
    [InlineData("unins000.exe")]
    [InlineData("UnInstall.exe")]
    [InlineData("vcredist_x64.exe")]
    [InlineData("dxsetup.exe")]
    [InlineData("EasyAntiCheat_Setup.exe")]
    [InlineData("GameUpdater.exe")]
    public void IsBlacklisted_MatchesKnownNonGameExecutables(string fileName)
    {
        var options = new ScanOptions();
        Assert.True(GameScanner.IsBlacklisted(fileName, options.BlacklistPatterns));
    }

    [Theory]
    [InlineData("HollowKnight.exe")]
    [InlineData("game.exe")]
    [InlineData("MyAwesomeGame.exe")]
    public void IsBlacklisted_DoesNotMatchLikelyGameExecutables(string fileName)
    {
        var options = new ScanOptions();
        Assert.False(GameScanner.IsBlacklisted(fileName, options.BlacklistPatterns));
    }

    [Fact]
    public void BuildCandidate_PicksMainGameExeOverInstallerJunk()
    {
        var folder = CreateGameFolder(
            "Hollow Knight",
            ("unins000.exe", 500_000),
            ("vcredist_x64.exe", 15_000_000),
            ("hollow_knight.exe", 50_000_000));

        var scanner = new GameScanner();
        var candidate = scanner.BuildCandidate(folder, new ScanOptions());

        Assert.NotNull(candidate.BestExePath);
        Assert.EndsWith("hollow_knight.exe", candidate.BestExePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCandidate_PrefersRootLevelExeOverDeeplyNestedOne()
    {
        var folder = CreateGameFolder(
            "MyGame",
            ("MyGame.exe", 10_000_000),
            (Path.Combine("x64", "bin", "engine.exe"), 10_000_000));

        var scanner = new GameScanner();
        var candidate = scanner.BuildCandidate(folder, new ScanOptions());

        Assert.NotNull(candidate.BestExePath);
        Assert.Equal(Path.Combine(folder, "MyGame.exe"), candidate.BestExePath);
    }

    [Fact]
    public void BuildCandidate_ReturnsNoCandidatesWhenOnlyBlacklistedExesExist()
    {
        var folder = CreateGameFolder(
            "JustAnInstaller",
            ("setup.exe", 1_000_000),
            ("unins000.exe", 500_000));

        var scanner = new GameScanner();
        var candidate = scanner.BuildCandidate(folder, new ScanOptions());

        Assert.Empty(candidate.ExeCandidates);
        Assert.False(candidate.IncludeByDefault);
    }

    [Fact]
    public void ScanFolder_ReturnsOneCandidatePerImmediateSubfolder()
    {
        CreateGameFolder("GameA", ("GameA.exe", 1_000_000));
        CreateGameFolder("GameB", ("GameB.exe", 1_000_000));

        var scanner = new GameScanner();
        var candidates = scanner.ScanFolder(_root);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.SuggestedName == "GameA");
        Assert.Contains(candidates, c => c.SuggestedName == "GameB");
    }

    [Fact]
    public void EnumerateExeFiles_RespectsMaxDepth()
    {
        var folder = CreateGameFolder(
            "DeepGame",
            (Path.Combine("a", "b", "c", "toodeep.exe"), 1_000_000));

        var found = GameScanner.EnumerateExeFiles(folder, maxDepth: 1).ToList();

        Assert.Empty(found);
    }
}
