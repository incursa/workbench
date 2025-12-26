using System.CommandLine;
using Spectre.Console;
using Workman.Git;

namespace Workman.Cli.Commands;

public class DoctorCommand : Command
{
    public DoctorCommand() : base("doctor", "Check environment and configuration")
    {
        this.SetHandler(ExecuteAsync);
    }

    private async Task<int> ExecuteAsync()
    {
        var gitService = new GitService();
        var allPassed = true;

        AnsiConsole.MarkupLine("[bold]Running environment checks...[/]");
        AnsiConsole.WriteLine();

        // Check Git installation
        var gitVersion = await gitService.GetGitVersionAsync();
        if (gitVersion != null)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Git is installed ({gitVersion})");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]✗[/] Git is not installed or not accessible");
            allPassed = false;
        }

        // Check if in Git repository
        var currentDir = Directory.GetCurrentDirectory();
        var isRepo = await gitService.IsGitRepositoryAsync(currentDir);
        if (isRepo)
        {
            var repoRoot = await gitService.GetRepositoryRootAsync(currentDir);
            AnsiConsole.MarkupLine($"[green]✓[/] Current directory is a Git repository ({repoRoot})");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] Current directory is not a Git repository");
        }

        // Check GitHub CLI (optional)
        var ghInstalled = await IsGhInstalledAsync();
        if (ghInstalled)
        {
            AnsiConsole.MarkupLine("[green]✓[/] GitHub CLI (gh) is installed");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] GitHub CLI (gh) not found - GitHub integration disabled");
        }

        AnsiConsole.WriteLine();

        if (allPassed)
        {
            AnsiConsole.MarkupLine("[green bold]Environment: OK[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow bold]Environment: Issues detected[/]");
            return 1;
        }
    }

    private async Task<bool> IsGhInstalledAsync()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "gh",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
