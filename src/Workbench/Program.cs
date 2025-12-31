namespace Workbench;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        if (args.Length > 0)
        {
            var command = args[0];
            if (string.Equals(command, "tui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "t", StringComparison.OrdinalIgnoreCase))
            {
                var tuiArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();
                return Workbench.Tui.TuiEntrypoint.RunAsync(tuiArgs);
            }
        }

        return Workbench.Cli.Program.RunAsync(args);
    }
}
