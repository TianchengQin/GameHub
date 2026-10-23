using System.Text.Json;
using GameHub.Core.Updates;
using Xunit;

namespace GameHub.Core.Tests;

public class UpdateVersionNormalizationTests
{
    private const string Json = """
        {"tag_name":"v1.2.0","assets":[{"name":"GameHubSetup.exe","browser_download_url":"https://github.com/x/y/GameHubSetup.exe","digest":"sha256:aa"}]}
        """;

    [Fact]
    public void FourPartAssemblyVersion_DoesNotSeeSameThreePartTagAsNewer()
    {
        using var doc = JsonDocument.Parse(Json);

        Assert.Null(UpdateChecker.ParseRelease(doc, new Version(1, 2, 0, 0)));
    }

    [Fact]
    public void NewerPatchTag_IsOfferedToFourPartAssemblyVersion()
    {
        using var doc = JsonDocument.Parse(Json.Replace("v1.2.0", "v1.2.1"));

        Assert.NotNull(UpdateChecker.ParseRelease(doc, new Version(1, 2, 0, 0)));
    }
}
