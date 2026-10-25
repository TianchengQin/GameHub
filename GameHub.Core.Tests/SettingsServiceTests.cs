using GameHub.Core.Settings;
using Xunit;

namespace GameHub.Core.Tests;

// AppPaths reads an environment variable, so keep these tests from running alongside anything else that uses it.
[Collection("DataDirEnvVar")]
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "GameHubSettingsTests_" + Guid.NewGuid());
    private readonly string? _previous = Environment.GetEnvironmentVariable(AppPaths.DataDirEnvVar);

    public SettingsServiceTests() => Environment.SetEnvironmentVariable(AppPaths.DataDirEnvVar, _dir);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AppPaths.DataDirEnvVar, _previous);
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Save_PersistsApiKey_AndNewInstanceReadsItBack()
    {
        var service = new SettingsService();
        service.Current.SteamGridDbApiKey = "0123456789abcdef0123456789abcdef";
        service.Current.MinimizeToTrayOnLaunch = false;

        Assert.True(service.Save());

        var reloaded = new SettingsService();
        Assert.Equal("0123456789abcdef0123456789abcdef", reloaded.Current.SteamGridDbApiKey);
        Assert.False(reloaded.Current.MinimizeToTrayOnLaunch);
        Assert.False(File.Exists(AppPaths.SettingsPath + ".tmp"));
    }

    [Fact]
    public void Save_ReturnsFalse_WhenTheDataFolderCannotBeUsed()
    {
        var blocker = Path.Combine(Path.GetTempPath(), "GameHubSettingsBlocker_" + Guid.NewGuid());
        File.WriteAllText(blocker, "i am a file, not a folder");
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirEnvVar, blocker);

            Assert.False(new SettingsService().Save());
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    [Fact]
    public void Load_FallsBackToDefaults_WhenFileIsCorrupt()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(AppPaths.SettingsPath, "{ this is not json");

        var service = new SettingsService();

        Assert.Null(service.Current.SteamGridDbApiKey);
        Assert.True(service.Current.AutoCheckUpdates);
    }
}
