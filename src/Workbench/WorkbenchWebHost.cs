using Microsoft.Extensions.FileProviders;
using Workbench.Core;

namespace Workbench;

public static class WorkbenchWebHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = WebOptions.Parse(args);
            var repoRoot = ResolveRepo(options.RepoPath);
            EnvLoader.LoadRepoEnv(repoRoot);

            var config = WorkbenchConfig.Load(repoRoot, out var configError);
            if (configError is not null)
            {
                await Console.Error.WriteLineAsync($"Config error: {configError}").ConfigureAwait(false);
                return 2;
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = options.RemainingArgs,
                ContentRootPath = repoRoot
            });

            ConfigureWebRoot(builder, repoRoot);
            builder.Services.AddRazorPages();
            builder.Services.AddSingleton(new WorkbenchWorkspace(repoRoot, config));
            builder.Services.AddSingleton<WorkbenchUserProfileStore>();
            builder.WebHost.UseUrls(options.Url);

            var app = builder.Build();
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.MapRazorPages();

            RegisterBrowserLaunch(app, options);

            await Console.Out.WriteLineAsync($"Workbench web UI listening on {options.Url}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Repo root: {repoRoot}").ConfigureAwait(false);

            await app.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(FormatError(ex)).ConfigureAwait(false);
            return 2;
        }
    }

    private static string ResolveRepo(string? repoArg)
    {
        var candidate = repoArg ?? Directory.GetCurrentDirectory();
        var repoRoot = Repository.FindRepoRoot(candidate);
        if (repoRoot is null)
        {
            throw new InvalidOperationException("Not a git repository.");
        }

        return repoRoot;
    }

    private static void ConfigureWebRoot(WebApplicationBuilder builder, string repoRoot)
    {
        var embeddedWebRootProvider = TryCreateEmbeddedWebRootProvider();
        var physicalWebRootPath = ResolvePhysicalWebRootPath(repoRoot);

        if (physicalWebRootPath is not null)
        {
            builder.Environment.WebRootPath = physicalWebRootPath;
            builder.Environment.WebRootFileProvider = embeddedWebRootProvider is null
                ? new PhysicalFileProvider(physicalWebRootPath)
                : new CompositeFileProvider(
                    new PhysicalFileProvider(physicalWebRootPath),
                    embeddedWebRootProvider);
            return;
        }

        if (embeddedWebRootProvider is not null)
        {
            builder.Environment.WebRootPath = builder.Environment.ContentRootPath;
            builder.Environment.WebRootFileProvider = embeddedWebRootProvider;
            return;
        }

        builder.Environment.WebRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    }

    private static string? ResolvePhysicalWebRootPath(string repoRoot)
    {
        var sourceWebRoot = Path.Combine(repoRoot, "src", "Workbench", "wwwroot");
        if (Directory.Exists(sourceWebRoot))
        {
            return sourceWebRoot;
        }

        var repoWebRoot = Path.Combine(repoRoot, "wwwroot");
        return Directory.Exists(repoWebRoot) ? repoWebRoot : null;
    }

    private static ManifestEmbeddedFileProvider? TryCreateEmbeddedWebRootProvider()
    {
        const string EmbeddedManifestName = "Microsoft.Extensions.FileProviders.Embedded.Manifest.xml";
        var assembly = typeof(Program).Assembly;

        return assembly.GetManifestResourceInfo(EmbeddedManifestName) is null
            ? null
            : new ManifestEmbeddedFileProvider(assembly, "wwwroot");
    }

    private static void RegisterBrowserLaunch(WebApplication app, WebOptions options)
    {
        if (!options.LaunchBrowser || !Environment.UserInteractive)
        {
            return;
        }

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var launchUrl = StartupBrowserLauncher.TryResolveLaunchUrl(app.Urls);
            if (launchUrl is null)
            {
                app.Logger.LogWarning("Browser launch skipped because no application URL was available.");
                return;
            }

            if (!StartupBrowserLauncher.TryLaunch(launchUrl, out var failureReason))
            {
                app.Logger.LogWarning("Browser launch failed for {LaunchUrl}. {FailureReason}", launchUrl, failureReason);
            }
        });
    }

    private static string FormatError(Exception ex)
    {
        return $"{ex.GetType().Name}: {ex.Message}";
    }

    private sealed record WebOptions(string? RepoPath, int Port, bool LaunchBrowser, string Url, string[] RemainingArgs)
    {
        public static WebOptions Parse(string[] args)
        {
            string? repoPath = null;
            var port = 5079;
            var launchBrowser = true;
            var remaining = new List<string>();

            var index = 0;
            while (index < args.Length)
            {
                var arg = args[index];

                if (string.Equals(arg, "--repo", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    repoPath = args[index + 1];
                    index += 2;
                    continue;
                }

                if (arg.StartsWith("--repo=", StringComparison.OrdinalIgnoreCase))
                {
                    repoPath = arg["--repo=".Length..];
                    index++;
                    continue;
                }

                if (string.Equals(arg, "-r", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    repoPath = args[index + 1];
                    index += 2;
                    continue;
                }

                if (string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    port = ParsePort(args[index + 1]);
                    index += 2;
                    continue;
                }

                if (arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase))
                {
                    port = ParsePort(arg["--port=".Length..]);
                    index++;
                    continue;
                }

                if (string.Equals(arg, "--no-open", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--no-browser", StringComparison.OrdinalIgnoreCase))
                {
                    launchBrowser = false;
                    index++;
                    continue;
                }

                if (string.Equals(arg, "--open", StringComparison.OrdinalIgnoreCase))
                {
                    launchBrowser = true;
                    index++;
                    continue;
                }

                remaining.Add(arg);
                index++;
            }

            return new WebOptions(repoPath, port, launchBrowser, $"http://127.0.0.1:{port}", remaining.ToArray());
        }

        private static int ParsePort(string value)
        {
            if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
            {
                throw new InvalidOperationException($"Invalid port: {value}");
            }

            return port;
        }
    }
}
