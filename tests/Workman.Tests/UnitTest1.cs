using Workman.Git;

namespace Workman.Tests;

public class GitServiceTests
{
    [Fact]
    public async Task IsGitInstalled_ShouldReturnTrue()
    {
        // Arrange
        var gitService = new GitService();

        // Act
        var isInstalled = await gitService.IsGitInstalledAsync();

        // Assert
        Assert.True(isInstalled, "Git should be installed in the test environment");
    }

    [Fact]
    public async Task GetGitVersion_ShouldReturnVersion()
    {
        // Arrange
        var gitService = new GitService();

        // Act
        var version = await gitService.GetGitVersionAsync();

        // Assert
        Assert.NotNull(version);
        Assert.Contains("git version", version);
    }
}