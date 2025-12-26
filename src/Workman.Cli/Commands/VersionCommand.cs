using System.CommandLine;
using System.Reflection;
using Spectre.Console;

namespace Workman.Cli.Commands;

public class VersionCommand : Command
{
    public VersionCommand() : base("version", "Display version information")
    {
        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "table",
            description: "Output format (table, json)");
        formatOption.AddAlias("-f");

        AddOption(formatOption);

        this.SetHandler(Execute, formatOption);
    }

    private void Execute(string format)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.1.0";

        var dotnetVersion = Environment.Version.ToString();
        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        if (format.ToLower() == "json")
        {
            var json = $$"""
            {
              "version": "{{version}}",
              "dotnet": "{{dotnetVersion}}",
              "runtime": "{{runtime}}"
            }
            """;
            AnsiConsole.WriteLine(json);
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Component")
                .AddColumn("Version");

            table.AddRow("Workman CLI", version);
            table.AddRow(".NET Runtime", runtime);

            AnsiConsole.Write(table);
        }
    }
}
