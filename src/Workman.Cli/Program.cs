using System.CommandLine;
using Workman.Cli.Commands;

namespace Workman.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Workman - Work Item Management CLI");

        // Add commands
        rootCommand.AddCommand(new VersionCommand());
        rootCommand.AddCommand(new DoctorCommand());

        return await rootCommand.InvokeAsync(args);
    }
}
