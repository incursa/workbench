namespace Workbench.IntegrationTests
{
    internal sealed class TempRepo : IDisposable
    {
        public TempRepo(string path)
        {
            this.Path = path;
        }

        public string Path { get; }

        public static TempRepo Create()
        {
            var repoRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);
            return new TempRepo(repoRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(this.Path))
                {
                    Directory.Delete(this.Path, recursive: true);
                }
            }
#pragma warning disable ERP022
            catch
            {
                // Best-effort cleanup.
            }
#pragma warning restore ERP022
        }
    }
}
