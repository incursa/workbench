using System.CommandLine;
using System.Text;

namespace Workbench.Cli;

public partial class Program
{
    private const string CliHelpContractPath = "contracts/commands.md";

    static void HandleCliHelpRegeneration(RootCommand root, string? repo, bool check, string? outputPath)
    {
        try
        {
            var repoRoot = ResolveRepo(repo);
            var path = ResolveCliHelpContractPath(repoRoot, outputPath);
            var generated = BuildCliHelpContract(root);
            var existing = File.Exists(path) ? File.ReadAllText(path) : null;
            var isCurrent = string.Equals(NormalizeGeneratedContent(existing), generated, StringComparison.Ordinal);

            if (check)
            {
                if (isCurrent)
                {
                    Console.WriteLine($"CLI help is up to date: {path}");
                    SetExitCode(0);
                    return;
                }

                Console.WriteLine($"CLI help drift detected: {path}");
                Console.WriteLine("Run `workbench doc regen-help` to regenerate contracts/commands.md.");
                SetExitCode(2);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? repoRoot);
            File.WriteAllText(path, generated);
            Console.WriteLine(isCurrent
                ? $"CLI help already current: {path}"
                : $"CLI help regenerated: {path}");
            SetExitCode(0);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            SetExitCode(2);
        }
    }

    static string BuildCliHelpContract(RootCommand root)
    {
        var builder = new StringBuilder();
        AppendLine(builder, "---");
        AppendLine(builder, "workbench:");
        AppendLine(builder, "  type: doc");
        AppendLine(builder, "  workItems: []");
        AppendLine(builder, "  codeRefs: []");
        AppendLine(builder, "  pathHistory:");
        AppendLine(builder, "    - \"C:/contracts/commands.md\"");
        AppendLine(builder, "  path: /contracts/commands.md");
        AppendLine(builder, "owner: platform");
        AppendLine(builder, "status: active");
        AppendLine(builder, "updated: 2025-12-27");
        AppendLine(builder, "---");
        AppendLine(builder);
        AppendLine(builder, "# Workbench CLI Help");
        AppendLine(builder);
        AppendLine(builder, "Generated from the live `System.CommandLine` tree.");
        AppendLine(builder, "Regenerate with `workbench doc regen-help`.");
        AppendLine(builder, "Verify drift with `workbench doc regen-help --check`.");
        AppendLine(builder);
        AppendLine(builder, "Machine-readable command output details remain documented in `contracts/commands.md`.");
        AppendLine(builder);
        AppendLine(builder, "## Usage");
        AppendLine(builder, "```text");
        AppendLine(builder, "workbench <command> [options]");
        AppendLine(builder, "```");
        AppendLine(builder);
        AppendLine(builder, "## Global options");
        AppendOptionList(builder, root.Options);
        AppendLine(builder);
        AppendLine(builder, "## Sync model");
        AppendLine(builder, "- `workbench sync`: umbrella command that runs the item, doc, and nav sync stages. Use this for the common happy path.");
        AppendLine(builder, "- `workbench item sync`: external sync stage for GitHub issues, imports, and branch state.");
        AppendLine(builder, "- `workbench doc sync`: repo metadata stage for doc/work-item backlinks and doc front matter.");
        AppendLine(builder, "- `workbench spec`: dedicated requirement-spec workflow for creation, inspection, editing, linking, unlinking, deletion, and sync.");
        AppendLine(builder, "- `workbench nav sync`: derived view stage for docs indexes, repo indexes, and the workboard.");
        AppendLine(builder, "- `workbench board regen`: narrow workboard-only regeneration when you do not need the broader nav stage.");
        AppendLine(builder);
        AppendLine(builder, "## Config");
        AppendLine(builder, "- Repo config path: `.workbench/config.json`.");
        AppendLine(builder);
        AppendLine(builder, "## Exit codes");
        AppendLine(builder, "- `0`: success, no warnings.");
        AppendLine(builder, "- `1`: success with warnings (`doctor` and `validate`).");
        AppendLine(builder, "- `2`: command failed due to errors.");
        AppendLine(builder);
        AppendLine(builder, "## Command tree");
        AppendCommandTree(builder, root, "workbench", 0);
        AppendLine(builder);
        AppendLine(builder, "## Detailed command reference");

        foreach (var subcommand in root.Subcommands.OrderBy(command => command.Name, StringComparer.Ordinal))
        {
            AppendCommandDetails(builder, subcommand, $"workbench {subcommand.Name}");
        }

        return builder.ToString();
    }

    private static void AppendCommandTree(StringBuilder builder, Command command, string path, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (command is RootCommand)
        {
            AppendLine(builder, $"- `{path}`");
        }
        else
        {
            AppendLine(builder, $"{indent}- `{path}`: {command.Description}");
        }

        foreach (var subcommand in command.Subcommands.OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            AppendCommandTree(builder, subcommand, $"{path} {subcommand.Name}", depth + 1);
        }
    }

    private static void AppendCommandDetails(StringBuilder builder, Command command, string path)
    {
        AppendLine(builder);
        AppendLine(builder, $"### `{path}`");
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            AppendLine(builder, command.Description);
        }

        var aliases = command.Aliases
            .Where(alias => !string.Equals(alias, command.Name, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToList();
        if (aliases.Count > 0)
        {
            AppendLine(builder);
            AppendLine(builder, $"Aliases: {string.Join(", ", aliases.Select(alias => $"`{alias}`"))}");
        }

        if (command.Arguments.Count > 0)
        {
            AppendLine(builder);
            AppendLine(builder, "Arguments:");
            foreach (var argument in command.Arguments)
            {
                var description = string.IsNullOrWhiteSpace(argument.Description)
                    ? "(no description)"
                    : argument.Description;
                AppendLine(builder, $"- `{argument.Name}`: {description}");
            }
        }

        if (command.Options.Count > 0)
        {
            AppendLine(builder);
            AppendLine(builder, "Options:");
            AppendOptionList(builder, command.Options);
        }

        var subcommands = command.Subcommands.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToList();
        if (subcommands.Count > 0)
        {
            AppendLine(builder);
            AppendLine(builder, "Subcommands:");
            foreach (var subcommand in subcommands)
            {
                AppendLine(builder, $"- `{subcommand.Name}`: {subcommand.Description}");
            }
        }

        foreach (var subcommand in subcommands)
        {
            AppendCommandDetails(builder, subcommand, $"{path} {subcommand.Name}");
        }
    }

    private static void AppendOptionList(StringBuilder builder, IEnumerable<Option> options)
    {
        foreach (var option in options)
        {
            var required = option.Required ? " (required)" : string.Empty;
            var description = string.IsNullOrWhiteSpace(option.Description)
                ? "(no description)"
                : option.Description;
            AppendLine(builder, $"- {FormatOptionDisplay(option)}{required}: {description}");
        }
    }

    private static string FormatOptionDisplay(Option option)
    {
        var aliases = option.Aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToList();

        if (aliases.Count == 0 && !string.IsNullOrWhiteSpace(option.Name))
        {
            aliases.Add(option.Name.StartsWith("-", StringComparison.Ordinal) ? option.Name : $"--{option.Name}");
        }

        if (aliases.Count == 0)
        {
            aliases.Add("(unnamed option)");
        }

        var valueHint = GetOptionValueHint(option);
        return string.Join(", ", aliases.Select(alias =>
            valueHint is null ? $"`{alias}`" : $"`{alias} <{valueHint}>`"));
    }

    private static string? GetOptionValueHint(Option option)
    {
        if (option.Arity.MaximumNumberOfValues == 0 || option.ValueType == typeof(bool))
        {
            return null;
        }

        var argumentName = option.Name;
        argumentName = string.IsNullOrWhiteSpace(argumentName) ? "value" : argumentName.Trim();
        return argumentName.Trim('<', '>', '-');
    }

    private static void AppendLine(StringBuilder builder, string? text = null)
    {
        if (text is not null)
        {
            builder.Append(text);
        }

        builder.Append('\n');
    }

    private static string NormalizeGeneratedContent(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        return content.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string ResolveCliHelpContractPath(string repoRoot, string? outputPath)
    {
        var target = string.IsNullOrWhiteSpace(outputPath) ? CliHelpContractPath : outputPath.Trim();
        return Path.IsPathRooted(target) ? target : Path.Combine(repoRoot, target);
    }
}
