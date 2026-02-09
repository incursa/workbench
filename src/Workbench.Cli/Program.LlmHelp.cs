// LLM-oriented help output that prints the full CLI surface in one stream.
// Designed for agent bootstrapping without requiring docs file discovery.
using System.CommandLine;

namespace Workbench.Cli;

public partial class Program
{
    static void WriteLlmHelp(RootCommand root)
    {
        Console.WriteLine("# Workbench LLM Help");
        Console.WriteLine();
        Console.WriteLine("Purpose: repo-native work item, docs, sync, and automation workflows.");
        Console.WriteLine("Agent defaults:");
        Console.WriteLine("- Prefer non-interactive CLI commands over TUI.");
        Console.WriteLine("- Use `--format json` for machine parsing.");
        Console.WriteLine("- Exit codes: 0=success, 1=success-with-warnings, 2=error.");
        Console.WriteLine();
        Console.WriteLine("Global options:");
        WriteOptions(root.Options);
        Console.WriteLine();
        Console.WriteLine("Command tree:");
        WriteCommandTree(root, "workbench", 0);
        Console.WriteLine();
        Console.WriteLine("Detailed command reference:");
        foreach (var subcommand in root.Subcommands.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            WriteCommandDetails(subcommand, $"workbench {subcommand.Name}");
        }
    }

    static void WriteCommandTree(Command command, string path, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (command is RootCommand)
        {
            Console.WriteLine($"- {path}");
        }
        else
        {
            Console.WriteLine($"{indent}- {path}: {command.Description}");
        }

        foreach (var subcommand in command.Subcommands.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            WriteCommandTree(subcommand, $"{path} {subcommand.Name}", depth + 1);
        }
    }

    static void WriteCommandDetails(Command command, string path)
    {
        Console.WriteLine();
        Console.WriteLine($"## {path}");
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            Console.WriteLine(command.Description);
        }

        var aliases = command.Aliases
            .Where(alias => !string.Equals(alias, command.Name, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (aliases.Count > 0)
        {
            Console.WriteLine($"Aliases: {string.Join(", ", aliases)}");
        }

        if (command.Arguments.Count > 0)
        {
            Console.WriteLine("Arguments:");
            foreach (var argument in command.Arguments)
            {
                var description = string.IsNullOrWhiteSpace(argument.Description) ? "(no description)" : argument.Description;
                Console.WriteLine($"- {argument.Name}: {description}");
            }
        }

        if (command.Options.Count > 0)
        {
            Console.WriteLine("Options:");
            WriteOptions(command.Options);
        }

        var subcommands = command.Subcommands.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        if (subcommands.Count > 0)
        {
            Console.WriteLine("Subcommands:");
            foreach (var subcommand in subcommands)
            {
                Console.WriteLine($"- {subcommand.Name}: {subcommand.Description}");
            }
        }

        foreach (var subcommand in subcommands)
        {
            WriteCommandDetails(subcommand, $"{path} {subcommand.Name}");
        }
    }

    static void WriteOptions(IEnumerable<Option> options)
    {
        foreach (var option in options)
        {
            var aliases = option.Aliases.Count > 0
                ? string.Join(", ", option.Aliases)
                : option.Name;
            var required = option.Required ? " (required)" : string.Empty;
            var description = string.IsNullOrWhiteSpace(option.Description) ? "(no description)" : option.Description;
            Console.WriteLine($"- {aliases}{required}: {description}");
        }
    }
}
