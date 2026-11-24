namespace Workbench.Providers.ClickUp.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        this.Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "workbench-clickup-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(this.Path))
        {
            Directory.Delete(this.Path, recursive: true);
        }
    }
}
