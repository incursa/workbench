namespace Workbench.IntegrationTests
{
    internal static class WorkbenchCli
    {
        private static readonly Lazy<string> dllPath = new(BuildWorkbenchCli);

        public static CommandResult Run(string workingDirectory, params string[] args)
        {
            return Run(workingDirectory, null, args);
        }

        public static CommandResult Run(
            string workingDirectory,
            IReadOnlyDictionary<string, string?>? environmentVariables,
            params string[] args)
        {
            var allArgs = new List<string> { WorkbenchCli.dllPath.Value };
            allArgs.AddRange(args);
            return ProcessRunner.Run(workingDirectory, "dotnet", environmentVariables, allArgs.ToArray());
        }

        private static string BuildWorkbenchCli()
        {
            var repoRoot = FindRepoRoot();
            var projectPath = System.IO.Path.Combine(repoRoot, "src", "Workbench", "Workbench.csproj");
            var buildResult = ProcessRunner.Run(repoRoot, "dotnet", "build", projectPath, "-c", "Debug");
            if (buildResult.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet build failed: {buildResult.StdErr}\n{buildResult.StdOut}");
            }

            var localDllPath = System.IO.Path.Combine(repoRoot, "src", "Workbench", "bin", "Debug", "net10.0", "Workbench.dll");
            if (!File.Exists(localDllPath))
            {
                throw new FileNotFoundException($"Workbench CLI not found at {localDllPath}.");
            }
            return localDllPath;
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(System.IO.Path.Combine(dir.FullName, "Workbench.slnx")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Workbench.slnx to locate repo root.");
        }
    }
}
