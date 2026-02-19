// Entry point for the Workbench CLI.
namespace Workbench;

public static class Program
{
    /// <summary>
    /// Main entry point for the Workbench binary.
    /// </summary>
    /// <param name="args">Command-line arguments passed from the host process.</param>
    /// <returns>Exit code.</returns>
    public static Task<int> Main(string[] args)
    {
        return Workbench.Cli.Program.RunAsync(args);
    }
}
