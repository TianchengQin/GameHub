using System.Collections.Concurrent;
using System.Diagnostics;

namespace GameHub.Core.Launching;

public class LaunchResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LaunchedAt { get; set; }
}

public class GameSessionEndedEventArgs : EventArgs
{
    public GameSessionEndedEventArgs(Guid gameId, DateTime launchedAt, long durationSeconds)
    {
        GameId = gameId;
        LaunchedAt = launchedAt;
        DurationSeconds = durationSeconds;
    }

    public Guid GameId { get; }
    public DateTime LaunchedAt { get; }
    public long DurationSeconds { get; }
}

/// <summary>
/// Starts game executables and tracks which games are currently running.
/// "LastPlayedAt" is recorded by the caller at the moment <see cref="Launch"/>
/// returns success, independent of whether the process can be tracked to exit
/// (many games hand off to a child process and exit their own launcher immediately).
/// </summary>
public class GameLauncher
{
    private readonly ConcurrentDictionary<Guid, Process> _runningProcesses = new();

    public event EventHandler<GameSessionEndedEventArgs>? SessionEnded;

    public bool IsRunning(Guid gameId) => _runningProcesses.ContainsKey(gameId);

    public LaunchResult Launch(Guid gameId, string executablePath, string? arguments = null, string? workingDirectory = null)
    {
        if (IsRunning(gameId))
            return new LaunchResult { Success = false, ErrorMessage = "游戏已在运行中。" };

        if (!File.Exists(executablePath))
            return new LaunchResult { Success = false, ErrorMessage = "找不到可执行文件，路径可能已失效。" };

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = !string.IsNullOrWhiteSpace(workingDirectory)
                ? workingDirectory
                : Path.GetDirectoryName(executablePath) ?? string.Empty,
            UseShellExecute = true
        };

        var launchedAt = DateTime.UtcNow;

        try
        {
            var process = Process.Start(startInfo);
            if (process is not null)
            {
                TryTrackProcess(gameId, process, launchedAt);
            }
        }
        catch (Exception ex)
        {
            return new LaunchResult { Success = false, ErrorMessage = ex.Message, LaunchedAt = launchedAt };
        }

        return new LaunchResult { Success = true, LaunchedAt = launchedAt };
    }

    private void TryTrackProcess(Guid gameId, Process process, DateTime launchedAt)
    {
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => OnProcessExited(gameId, launchedAt, process);

            if (process.HasExited)
            {
                OnProcessExited(gameId, launchedAt, process);
                return;
            }

            _runningProcesses[gameId] = process;
        }
        catch (InvalidOperationException)
        {
            // Process already exited before we could attach; nothing to track.
        }
    }

    private void OnProcessExited(Guid gameId, DateTime launchedAt, Process process)
    {
        _runningProcesses.TryRemove(gameId, out _);
        var durationSeconds = Math.Max(0L, (long)(DateTime.UtcNow - launchedAt).TotalSeconds);
        SessionEnded?.Invoke(this, new GameSessionEndedEventArgs(gameId, launchedAt, durationSeconds));
        process.Dispose();
    }
}
