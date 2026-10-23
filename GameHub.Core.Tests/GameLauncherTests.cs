using GameHub.Core.Launching;
using Xunit;

namespace GameHub.Core.Tests;

public class GameLauncherTests
{
    [Fact]
    public void Launch_ReturnsFailure_WhenExecutableDoesNotExist()
    {
        var launcher = new GameLauncher();
        var missingPath = Path.Combine(Path.GetTempPath(), "GameHubTests_missing_" + Guid.NewGuid() + ".exe");

        var result = launcher.Launch(Guid.NewGuid(), missingPath);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void IsRunning_IsFalse_ForUnknownGameId()
    {
        var launcher = new GameLauncher();

        Assert.False(launcher.IsRunning(Guid.NewGuid()));
    }
}
