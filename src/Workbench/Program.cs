namespace Workbench;

public static class Program
{
    /// <summary>
    /// Main entry point for the Workbench binary.
    /// </summary>
    /// <param name="args">Command-line arguments passed from the host process.</param>
    /// <returns>Exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        if (TryConsumeWebMode(ref args))
        {
            return await WorkbenchWebHost.RunAsync(args).ConfigureAwait(false);
        }

        return await Workbench.Cli.Program.RunAsync(args).ConfigureAwait(false);
    }

    private static bool TryConsumeWebMode(ref string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        if (!string.Equals(args[0], "web", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(args[0], "ui", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        args = args.Length == 1 ? Array.Empty<string>() : args[1..];
        return true;
    }
}
