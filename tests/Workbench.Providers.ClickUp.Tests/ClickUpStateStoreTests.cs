namespace Workbench.Providers.ClickUp.Tests;

[TestClass]
public sealed class ClickUpStateStoreTests
{
    [TestMethod]
    public async Task ApplyLeaseSerializesProviderProcessesSharingState()
    {
        using var temporary = new TemporaryDirectory();
        var firstStore = new ClickUpStateStore(temporary.Path);
        var secondStore = new ClickUpStateStore(temporary.Path);
        await using var firstLease = await firstStore.AcquireApplyLeaseAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var secondLeaseTask = secondStore.AcquireApplyLeaseAsync(timeout.Token);

        await Task.Delay(100, timeout.Token);
        Assert.IsFalse(secondLeaseTask.IsCompleted);
        await firstLease.DisposeAsync();
        await using var secondLease = await secondLeaseTask;

        Assert.IsNotNull(secondLease);
    }
}
