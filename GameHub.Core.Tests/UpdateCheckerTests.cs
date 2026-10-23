using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameHub.Core.Updates;
using Xunit;

namespace GameHub.Core.Tests;

public class UpdateCheckerTests
{
    private static JsonDocument Release(string tag, string assetsJson, string body = "", bool prerelease = false, bool draft = false)
        => JsonDocument.Parse($$"""
            {"tag_name":"{{tag}}","body":{{JsonSerializer.Serialize(body)}},"html_url":"https://github.com/x/y/releases/tag/{{tag}}",
             "prerelease":{{prerelease.ToString().ToLowerInvariant()}},"draft":{{draft.ToString().ToLowerInvariant()}},"assets":[{{assetsJson}}]}
            """);

    private const string InstallerAsset = """
        {"name":"GameHubSetup-1.3.0.exe","browser_download_url":"https://github.com/x/y/releases/download/v1.3.0/GameHubSetup-1.3.0.exe","digest":"sha256:ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef0123456789"}
        """;

    [Theory]
    [InlineData("v1.2.0", true, "1.2.0")]
    [InlineData("1.10.3", true, "1.10.3")]
    [InlineData("V2.0.0-beta.1", true, "2.0.0")]
    [InlineData("release-final", false, "0.0")]
    [InlineData("", false, "0.0")]
    public void TryParseVersion_HandlesCommonTagFormats(string tag, bool ok, string expected)
    {
        Assert.Equal(ok, UpdateChecker.TryParseVersion(tag, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Fact]
    public void ParseRelease_ReturnsUpdate_WhenNewer_AndReadsDigest()
    {
        using var doc = Release("v1.3.0", InstallerAsset, body: "notes");

        var update = UpdateChecker.ParseRelease(doc, new Version(1, 2, 0));

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 3, 0), update!.Version);
        Assert.EndsWith("GameHubSetup-1.3.0.exe", update.DownloadUrl);
        Assert.Equal("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", update.Sha256);
    }

    [Fact]
    public void ParseRelease_ReturnsNull_WhenSameOrOlder()
    {
        using var same = Release("v1.2.0", InstallerAsset);
        using var older = Release("v1.1.0", InstallerAsset);

        Assert.Null(UpdateChecker.ParseRelease(same, new Version(1, 2, 0)));
        Assert.Null(UpdateChecker.ParseRelease(older, new Version(1, 2, 0)));
    }

    [Fact]
    public void ParseRelease_IgnoresDraftsPrereleasesAndMissingInstaller()
    {
        using var draft = Release("v9.0.0", InstallerAsset, draft: true);
        using var pre = Release("v9.0.0", InstallerAsset, prerelease: true);
        using var noAsset = Release("v9.0.0", """{"name":"source.zip","browser_download_url":"https://github.com/x/y/a.zip"}""");

        Assert.Null(UpdateChecker.ParseRelease(draft, new Version(1, 0)));
        Assert.Null(UpdateChecker.ParseRelease(pre, new Version(1, 0)));
        Assert.Null(UpdateChecker.ParseRelease(noAsset, new Version(1, 0)));
    }

    [Fact]
    public void ParseRelease_FallsBackToHashInReleaseNotes()
    {
        var hash = new string('a', 64);
        using var doc = Release("v1.3.0",
            """{"name":"GameHubSetup.exe","browser_download_url":"https://github.com/x/y/GameHubSetup.exe"}""",
            body: $"Changes...\nSHA-256: {hash}");

        var update = UpdateChecker.ParseRelease(doc, new Version(1, 2, 0));

        Assert.Equal(hash, update!.Sha256);
    }

    [Theory]
    [InlineData("https://github.com/a/b/releases/download/v1/x.exe", true)]
    [InlineData("https://objects.githubusercontent.com/x", true)]
    [InlineData("http://github.com/a/b/x.exe", false)]
    [InlineData("https://github.com.evil.example/x.exe", false)]
    [InlineData("https://evil.example/github.com/x.exe", false)]
    public void IsTrustedDownloadHost_OnlyAllowsHttpsGitHub(string url, bool expected)
        => Assert.Equal(expected, UpdateChecker.IsTrustedDownloadHost(url));

    [Fact]
    public void ParseRelease_RejectsInstallerFromUntrustedHost()
    {
        using var doc = Release("v1.3.0", """{"name":"GameHubSetup.exe","browser_download_url":"https://evil.example/GameHubSetup.exe","digest":"sha256:abc"}""");

        Assert.Null(UpdateChecker.ParseRelease(doc, new Version(1, 2, 0)));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;
        public FakeHandler(byte[] payload) => _payload = payload;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_payload) });
    }

    [Fact]
    public async Task DownloadInstallerAsync_ReturnsFile_WhenHashMatches()
    {
        var payload = Encoding.UTF8.GetBytes("pretend installer " + Guid.NewGuid());
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var info = new UpdateInfo(new Version(9, 9, 9), "v9.9.9", "https://github.com/x/y/GameHubSetup.exe", hash, "", "");
        var checker = new UpdateChecker(new HttpClient(new FakeHandler(payload)));

        var path = await checker.DownloadInstallerAsync(info);

        try { Assert.Equal(payload, await File.ReadAllBytesAsync(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DownloadInstallerAsync_DeletesFileAndThrows_WhenHashMismatches()
    {
        var info = new UpdateInfo(new Version(9, 9, 8), "v9.9.8", "https://github.com/x/y/GameHubSetup.exe", new string('0', 64), "", "");
        var checker = new UpdateChecker(new HttpClient(new FakeHandler(Encoding.UTF8.GetBytes("tampered"))));

        await Assert.ThrowsAsync<InvalidDataException>(() => checker.DownloadInstallerAsync(info));

        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "GameHubUpdate", "GameHubSetup-9.9.8.exe")));
    }

    [Fact]
    public async Task DownloadInstallerAsync_Refuses_WhenNoHashIsKnown()
    {
        var info = new UpdateInfo(new Version(9, 9, 7), "v9.9.7", "https://github.com/x/y/GameHubSetup.exe", null, "", "");
        var checker = new UpdateChecker(new HttpClient(new FakeHandler(new byte[] { 1 })));

        await Assert.ThrowsAsync<InvalidOperationException>(() => checker.DownloadInstallerAsync(info));
    }
}
