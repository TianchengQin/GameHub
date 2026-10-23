using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameHub.Core.Updates;

public record UpdateInfo(Version Version, string Tag, string DownloadUrl, string? Sha256, string ReleaseNotes, string PageUrl);

/// <summary>Looks for a newer GameHub release on GitHub and downloads its installer.</summary>
public class UpdateChecker
{
    public const string InstallerAssetPrefix = "GameHubSetup";

    private static readonly Regex Sha256InText = new(@"sha-?256\W{0,6}([0-9a-fA-F]{64})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly string _latestReleaseUrl;

    public UpdateChecker(HttpClient http, string owner = "TianchengQin", string repo = "GameHub")
    {
        _http = http;
        _latestReleaseUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("GameHub-Updater");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <summary>Returns the newer release, or null when already up to date or no usable installer is attached.</summary>
    public async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(_latestReleaseUrl, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null; // no release published yet

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return ParseRelease(doc, current);
    }

    public static UpdateInfo? ParseRelease(JsonDocument doc, Version current)
    {
        var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;

        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        if (!TryParseVersion(tag, out var version) || Normalize(version) <= Normalize(current)) return null;

        var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? string.Empty : string.Empty;
        var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? string.Empty : string.Empty;

        string? downloadUrl = null;
        string? sha = null;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                if (name is null || url is null) continue;

                if (name.StartsWith(InstallerAssetPrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = url;
                    var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
                    if (digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        sha = digest["sha256:".Length..];
                }
            }
        }

        if (downloadUrl is null || !IsTrustedDownloadHost(downloadUrl)) return null;

        sha ??= Sha256InText.Match(body) is { Success: true } m ? m.Groups[1].Value : null;
        return new UpdateInfo(version, tag!, downloadUrl, sha?.ToLowerInvariant(), body, page);
    }

    /// <summary>1.2.0 and 1.2.0.0 must compare equal (Version treats a missing component as -1).</summary>
    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    public static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var cleaned = tag.Trim().TrimStart('v', 'V');
        var dash = cleaned.IndexOfAny(new[] { '-', '+' });
        if (dash >= 0) cleaned = cleaned[..dash];

        if (!Version.TryParse(cleaned, out var parsed)) return false;
        version = parsed;
        return true;
    }

    public static bool IsTrustedDownloadHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;

        var host = uri.Host;
        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Downloads the installer to a temp folder and verifies its SHA-256. Throws if the hash is unknown or wrong.</summary>
    public async Task<string> DownloadInstallerAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(update.Sha256))
            throw new InvalidOperationException("该版本没有提供校验值(SHA-256)，为安全起见不会自动安装，请到发布页手动下载。");

        var folder = Path.Combine(Path.GetTempPath(), "GameHubUpdate");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, $"GameHubSetup-{update.Version}.exe");

        using (var response = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(target);

            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;
                if (total is > 0) progress?.Report((double)received / total.Value);
            }
        }

        var actual = await ComputeSha256Async(target, ct);
        if (!actual.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(target);
            throw new InvalidDataException("安装包校验失败（SHA-256 不匹配），已丢弃下载的文件。");
        }

        return target;
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
