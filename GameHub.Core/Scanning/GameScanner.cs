using System.Text;
using System.Text.RegularExpressions;

namespace GameHub.Core.Scanning;

/// <summary>
/// Scans a root folder's immediate subfolders for likely game executables.
/// Each subfolder becomes one <see cref="ScanCandidate"/> with its exe files
/// filtered against a blacklist and ranked by a name/depth/size heuristic.
/// </summary>
public class GameScanner
{
    public List<ScanCandidate> ScanFolder(string rootPath, ScanOptions? options = null)
    {
        options ??= new ScanOptions();
        var result = new List<ScanCandidate>();

        if (!Directory.Exists(rootPath))
            return result;

        foreach (var subDir in Directory.EnumerateDirectories(rootPath))
        {
            result.Add(BuildCandidate(subDir, options));
        }

        return result;
    }

    private static readonly string[] ShortcutNameNoise =
    {
        "*uninstall*", "*readme*", "*help*", "*website*", "*manual*", "*changelog*", "*release notes*",
        "*卸载*", "*帮助*", "*说明*", "*官网*", "*更新日志*"
    };

    /// <summary>Lists exe targets of Start Menu shortcuts (all-users + current user) as import candidates.</summary>
    public List<ScanCandidate> ScanStartMenu(ScanOptions? options = null)
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
        };

        var shortcuts = roots.SelectMany(EnumerateShortcutFiles);
        return ScanShortcuts(shortcuts, ShortcutResolver.ResolveTarget, options);
    }

    public List<ScanCandidate> ScanShortcuts(IEnumerable<string> lnkPaths, Func<string, string?> resolveTarget, ScanOptions? options = null)
    {
        options ??= new ScanOptions();
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ScanCandidate>();

        foreach (var lnkPath in lnkPaths)
        {
            var linkName = Path.GetFileNameWithoutExtension(lnkPath);
            if (ShortcutNameNoise.Any(p => IsWildcardMatch(linkName, p))) continue;

            var target = resolveTarget(lnkPath);
            if (string.IsNullOrWhiteSpace(target)) continue;
            if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            if (target.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase)) continue;
            if (!File.Exists(target)) continue;
            if (IsBlacklisted(Path.GetFileName(target), options.BlacklistPatterns)) continue;
            if (!seenTargets.Add(target)) continue;

            results.Add(new ScanCandidate
            {
                FolderPath = Path.GetDirectoryName(target) ?? string.Empty,
                SuggestedName = linkName,
                ExeCandidates = new List<ExeCandidate> { new() { FullPath = target, Score = 1.0 } }
            });
        }

        return results.OrderBy(c => c.SuggestedName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static IEnumerable<string> EnumerateShortcutFiles(string root)
    {
        if (!Directory.Exists(root)) return Enumerable.Empty<string>();

        try
        {
            return Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Enumerable.Empty<string>();
        }
    }

    public ScanCandidate BuildCandidate(string folderPath, ScanOptions options)
    {
        var folderName = new DirectoryInfo(folderPath).Name;
        var candidate = new ScanCandidate
        {
            FolderPath = folderPath,
            SuggestedName = folderName
        };

        var exeFiles = EnumerateExeFiles(folderPath, options.MaxDepth)
            .Where(f => !IsBlacklisted(Path.GetFileName(f), options.BlacklistPatterns))
            .ToList();

        if (exeFiles.Count == 0)
            return candidate;

        var sizes = new Dictionary<string, long>();
        long maxSize = 0;
        foreach (var exe in exeFiles)
        {
            long size;
            try
            {
                size = new FileInfo(exe).Length;
            }
            catch
            {
                size = 0;
            }

            sizes[exe] = size;
            if (size > maxSize) maxSize = size;
        }

        candidate.ExeCandidates = exeFiles
            .Select(exe => new ExeCandidate
            {
                FullPath = exe,
                Score = ScoreExe(exe, folderPath, folderName, sizes[exe], maxSize)
            })
            .OrderByDescending(c => c.Score)
            .ToList();

        return candidate;
    }

    public static IEnumerable<string> EnumerateExeFiles(string folderPath, int maxDepth)
        => EnumerateExeFilesRecursive(folderPath, 0, maxDepth);

    private static IEnumerable<string> EnumerateExeFilesRecursive(string folderPath, int depth, int maxDepth)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folderPath, "*.exe");
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
            yield return file;

        if (depth >= maxDepth)
            yield break;

        IEnumerable<string> subDirs;
        try
        {
            subDirs = Directory.EnumerateDirectories(folderPath);
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var subDir in subDirs)
        {
            foreach (var file in EnumerateExeFilesRecursive(subDir, depth + 1, maxDepth))
                yield return file;
        }
    }

    public static bool IsBlacklisted(string fileName, IEnumerable<string> patterns)
        => patterns.Any(pattern => IsWildcardMatch(fileName, pattern));

    private static bool IsWildcardMatch(string input, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }

    public static double ScoreExe(string exePath, string rootFolderPath, string folderName, long size, long maxSizeInFolder)
    {
        var exeBaseName = Path.GetFileNameWithoutExtension(exePath);
        var nameSimilarity = NameSimilarity(folderName, exeBaseName);

        var depth = GetRelativeDepth(rootFolderPath, exePath);
        var depthScore = Math.Max(0.0, 1.0 - depth * 0.25);

        var sizeScore = maxSizeInFolder > 0 ? (double)size / maxSizeInFolder : 0.0;

        return nameSimilarity * 0.5 + depthScore * 0.3 + sizeScore * 0.2;
    }

    private static int GetRelativeDepth(string rootFolderPath, string exePath)
    {
        var rootFull = Path.GetFullPath(rootFolderPath);
        var exeDir = Path.GetFullPath(Path.GetDirectoryName(exePath) ?? rootFull);

        if (string.Equals(rootFull, exeDir, StringComparison.OrdinalIgnoreCase))
            return 0;

        var relative = Path.GetRelativePath(rootFull, exeDir);
        return relative
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Count(p => p.Length > 0 && p != ".");
    }

    private static double NameSimilarity(string folderName, string exeBaseName)
    {
        var a = Normalize(folderName);
        var b = Normalize(exeBaseName);

        if (a.Length == 0 || b.Length == 0)
            return 0.0;

        if (a == b)
            return 1.0;

        if (a.Contains(b) || b.Contains(a))
            return 0.85;

        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return Math.Max(0.0, 1.0 - (double)distance / maxLen);
    }

    private static string Normalize(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }
}
