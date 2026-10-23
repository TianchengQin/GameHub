using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using GameHub.Core.Updates;

namespace GameHub.App.Updates;

/// <summary>Owns the current version, the latest check result, and the "download + run installer" flow.</summary>
public class UpdateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly UpdateChecker _checker = new(Http);

    public Version CurrentVersion { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0, 0);

    public string CurrentVersionText => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{Math.Max(CurrentVersion.Build, 0)}";

    public UpdateInfo? Available { get; private set; }

    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        Available = await _checker.CheckAsync(CurrentVersion, ct);
        return Available;
    }

    /// <summary>Downloads and verifies the installer, then starts it silently; the caller should exit the app afterwards.</summary>
    public async Task LaunchInstallerAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken ct = default)
    {
        var installer = await _checker.DownloadInstallerAsync(update, progress, ct);

        Process.Start(new ProcessStartInfo(installer, "/SILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /RELAUNCH")
        {
            UseShellExecute = true
        });
    }
}
