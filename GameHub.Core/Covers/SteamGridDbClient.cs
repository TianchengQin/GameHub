using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GameHub.Core.Covers;

public record SteamGridDbGame(int Id, string Name);

public record SteamGridDbImage(string Url, string ThumbUrl);

/// <summary>Minimal client for the SteamGridDB v2 API (requires a user-supplied API key).</summary>
public sealed class SteamGridDbClient : IDisposable
{
    private readonly HttpClient _http;

    public SteamGridDbClient(string apiKey)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://www.steamgriddb.com/api/v2/"), Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<List<SteamGridDbGame>> SearchGamesAsync(string term, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync($"search/autocomplete/{Uri.EscapeDataString(term)}", ct);
        return ParseGames(doc);
    }

    public async Task<List<SteamGridDbImage>> GetGridsAsync(int gameId, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync($"grids/game/{gameId}?dimensions=600x900,342x482&types=static", ct);
        return ParseImages(doc);
    }

    public Task<byte[]> DownloadImageAsync(string url, CancellationToken ct = default)
        => _http.GetByteArrayAsync(url, ct);

    public static List<SteamGridDbGame> ParseGames(JsonDocument doc)
    {
        var results = new List<SteamGridDbGame>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var item in data.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && item.TryGetProperty("name", out var name))
                results.Add(new SteamGridDbGame(id.GetInt32(), name.GetString() ?? string.Empty));
        }

        return results;
    }

    public static List<SteamGridDbImage> ParseImages(JsonDocument doc)
    {
        var results = new List<SteamGridDbImage>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var item in data.EnumerateArray())
        {
            var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (string.IsNullOrEmpty(url)) continue;

            var thumb = item.TryGetProperty("thumb", out var t) ? t.GetString() : null;
            results.Add(new SteamGridDbImage(url, string.IsNullOrEmpty(thumb) ? url : thumb));
        }

        return results;
    }

    private async Task<JsonDocument> GetJsonAsync(string relativeUrl, CancellationToken ct)
    {
        using var response = await _http.GetAsync(relativeUrl, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("API Key 无效或已过期，请到设置页检查。");

        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public void Dispose() => _http.Dispose();
}
