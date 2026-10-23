using System.Text.Json;
using GameHub.Core.Covers;
using Xunit;

namespace GameHub.Core.Tests;

public class SteamGridDbParsingTests
{
    [Fact]
    public void ParseGames_ReadsIdAndName()
    {
        using var doc = JsonDocument.Parse("""{"success":true,"data":[{"id":5254,"name":"Hades"},{"id":1,"name":"Other"}]}""");

        var games = SteamGridDbClient.ParseGames(doc);

        Assert.Equal(2, games.Count);
        Assert.Equal(new SteamGridDbGame(5254, "Hades"), games[0]);
    }

    [Fact]
    public void ParseImages_FallsBackToFullUrlWhenThumbMissing_AndSkipsEntriesWithoutUrl()
    {
        using var doc = JsonDocument.Parse("""
            {"data":[
              {"url":"https://cdn/a.png","thumb":"https://cdn/a_t.png"},
              {"url":"https://cdn/b.png"},
              {"thumb":"https://cdn/c_t.png"}
            ]}
            """);

        var images = SteamGridDbClient.ParseImages(doc);

        Assert.Equal(2, images.Count);
        Assert.Equal("https://cdn/a_t.png", images[0].ThumbUrl);
        Assert.Equal("https://cdn/b.png", images[1].ThumbUrl);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenDataMissing()
    {
        using var doc = JsonDocument.Parse("""{"success":false}""");

        Assert.Empty(SteamGridDbClient.ParseGames(doc));
        Assert.Empty(SteamGridDbClient.ParseImages(doc));
    }
}
