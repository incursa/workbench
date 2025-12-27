namespace Workbench.IntegrationTests
{
    internal static class ProcessRunner
    {
        public static CommandResult Run(string workingDirectory, string fileName, params string[] args)
        {
            var psi = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new CommandResult(process.ExitCode, stdout.Trim(), stderr.Trim());
        }
    }
}
