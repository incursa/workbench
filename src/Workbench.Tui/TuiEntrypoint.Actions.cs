using Terminal.Gui;
using Terminal.Gui.Trees;
using Workbench;
using Workbench.Core;

namespace Workbench.Tui;

public static partial class TuiEntrypoint
{
    private static void UpdateGitInfo(TuiContext context)
    {
        var gitInfoLabel = context.GitInfoLabel;
        if (gitInfoLabel is null)
        {
            return;
        }

        try
        {
            var branch = GitService.GetCurrentBranch(context.RepoRoot);
            var clean = GitService.IsClean(context.RepoRoot);
            gitInfoLabel.Text = $"Git: {branch} {(clean ? "clean" : "dirty")}";
        }
        catch
        {
            gitInfoLabel.Text = "Git: unavailable";
#pragma warning disable ERP022
        }
#pragma warning restore ERP022
    }

    private static void UpdateLastRefreshLabel(TuiContext context)
    {
        var lastRefreshLabel = context.LastRefreshLabel;
        if (lastRefreshLabel is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        lastRefreshLabel.Text = $"Updated: {now:HH:mm:ss}";
    }

    private static void ConfigureAutoRefresh(TuiContext context, Action refreshAction)
    {
        if (context.AutoRefreshToken is not null)
        {
            Application.MainLoop.RemoveTimeout(context.AutoRefreshToken);
            context.AutoRefreshToken = null;
        }

        if (context.Config.Tui.AutoRefreshSeconds <= 0)
        {
            return;
        }

        context.AutoRefreshToken = Application.MainLoop.AddTimeout(
            TimeSpan.FromSeconds(context.Config.Tui.AutoRefreshSeconds),
            _ =>
            {
                refreshAction();
                return true;
            });
    }

    private static void ShowDocPreviewDialog(TuiContext context, string path, string resolvedPath, string content)
    {
        var dialog = new Dialog($"Preview: {path}", 0, 0)
        {
            Width = Dim.Fill(2),
            Height = Dim.Fill(2)
        };
        var preview = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(3),
            ReadOnly = true,
            WordWrap = true,
            Text = content
        };
        dialog.Add(preview);

        void OpenExternal()
        {
            SetCommandPreview(context, $"open \"{resolvedPath}\"");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = resolvedPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        var openButton = new Button("Open External");
        openButton.Clicked += OpenExternal;

        var closeButton = new Button("Close");
        closeButton.Clicked += () => Application.RequestStop();
        dialog.AddButton(openButton);
        dialog.AddButton(closeButton);

        dialog.KeyDown += args =>
        {
            if (args.KeyEvent.Key == Key.O || args.KeyEvent.Key == (Key.O | Key.CtrlMask))
            {
                OpenExternal();
                args.Handled = true;
            }
        };

        Application.Run(dialog);
    }

    private static bool TryShowPreviewForLink(TuiContext context, string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out _))
        {
            return false;
        }

        var trimmedLink = link;
        var anchorIndex = trimmedLink.IndexOf('#', StringComparison.Ordinal);
        if (anchorIndex >= 0)
        {
            trimmedLink = trimmedLink[..anchorIndex];
        }
        var queryIndex = trimmedLink.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            trimmedLink = trimmedLink[..queryIndex];
        }

        if (!trimmedLink.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var resolved = ResolveLink(context.RepoRoot, trimmedLink);
        if (!File.Exists(resolved))
        {
            ShowInfo("Doc not found.");
            return true;
        }

        try
        {
            var content = File.ReadAllText(resolved);
            SetCommandPreview(context, $"preview \"{resolved}\"");
            ShowDocPreviewDialog(context, trimmedLink, resolved, content);
            return true;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return true;
        }
    }

    private static WorkItem? GetSelectedItem(TuiContext context)
    {
        var listView = context.ListView;
        if (listView is null)
        {
            return null;
        }

        if (listView.SelectedItem < 0 || listView.SelectedItem >= context.ListItemLookup.Count)
        {
            return null;
        }

        return context.ListItemLookup[listView.SelectedItem];
    }

    private static string ApplyPattern(string pattern, WorkItem item)
    {
        return pattern
            .Replace("{id}", item.Id, StringComparison.Ordinal)
            .Replace("{slug}", item.Slug, StringComparison.Ordinal)
            .Replace("{title}", item.Title, StringComparison.Ordinal);
    }

    private static int FindNextItemIndex(TuiContext context, int startIndex)
    {
        for (var i = startIndex + 1; i < context.ListItemLookup.Count; i++)
        {
            if (context.ListItemLookup[i] is not null)
            {
                return i;
            }
        }

        for (var i = startIndex - 1; i >= 0; i--)
        {
            if (context.ListItemLookup[i] is not null)
            {
                return i;
            }
        }

        return -1;
    }

    private static void UpdateCodexStartState(TuiContext context, WorkItem? item)
    {
        var codexWorkButton = context.CodexWorkButton!;
        var codexHintLabel = context.CodexHintLabel!;

        if (!context.CodexAvailable)
        {
            codexWorkButton.Enabled = false;
            codexHintLabel.Text = string.IsNullOrWhiteSpace(context.CodexError)
                ? "Codex CLI unavailable."
                : $"Codex unavailable: {context.CodexError}";
            return;
        }

        if (item is null)
        {
            codexWorkButton.Enabled = false;
            codexHintLabel.Text = "Select a work item to start Codex.";
            return;
        }

        try
        {
            if (!GitService.IsClean(context.RepoRoot))
            {
                codexWorkButton.Enabled = false;
                codexHintLabel.Text = "Commit or stash changes to start Codex.";
                return;
            }
        }
        catch (Exception ex)
        {
            codexWorkButton.Enabled = false;
            codexHintLabel.Text = $"Git unavailable: {ex}";
            return;
        }

        codexWorkButton.Enabled = true;
        codexHintLabel.Text = string.IsNullOrWhiteSpace(context.CodexVersion)
            ? "Codex ready."
            : $"Codex {context.CodexVersion}";
    }

    private static void UpdateDetails(TuiContext context, int index)
    {
        var listItemLookup = context.ListItemLookup;
        var linkTargets = context.LinkTargets;
        var detailsHeader = context.DetailsHeader!;
        var detailsBody = context.DetailsBody!;
        var linksList = context.LinksList!;
        var linkHint = context.LinkHint!;
        var linkTypeField = context.LinkTypeField!;
        var startWorkButton = context.StartWorkButton!;
        var completeWorkButton = context.CompleteWorkButton!;

        if (index < 0 || index >= listItemLookup.Count)
        {
            detailsHeader.Text = "Select a work item to see details.";
            detailsBody.Text = string.Empty;
            linkTargets.Clear();
            linksList.SetSource(new List<string>());
            linkHint.Text = string.Empty;
            SetCommandPreview(context, "(none)");
            startWorkButton.Enabled = false;
            completeWorkButton.Enabled = false;
            UpdateCodexStartState(context, null);
            return;
        }

        var item = listItemLookup[index];
        if (item is null)
        {
            detailsHeader.Text = "Select a work item to see details.";
            detailsBody.Text = string.Empty;
            linkTargets.Clear();
            linksList.SetSource(new List<string>());
            linkHint.Text = string.Empty;
            SetCommandPreview(context, "(none)");
            startWorkButton.Enabled = false;
            completeWorkButton.Enabled = false;
            UpdateCodexStartState(context, null);
            return;
        }
        var specsCount = item.Related.Specs.Count;
        var adrsCount = item.Related.Adrs.Count;
        var filesCount = item.Related.Files.Count;
        var issuesCount = item.Related.Issues.Count;
        var prsCount = item.Related.Prs.Count;

        detailsHeader.Text = $"[{item.Status}] {item.Id}\n{item.Title}\n\n\nOwner: {item.Owner ?? "?"}\nPriority: {item.Priority ?? "?"}\nUpdated: {item.Updated ?? "?"}\n\nLinked: specs {specsCount}, adrs {adrsCount}, files {filesCount}\nIssues: {issuesCount}, PRs: {prsCount}\n\nEnter: open selected link";
        detailsBody.Text = item.Body;
        SetCommandPreview(context, $"workbench item show {item.Id}");
        startWorkButton.Enabled = true;
        completeWorkButton.Enabled = !string.Equals(item.Status, "done", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.Status, "dropped", StringComparison.OrdinalIgnoreCase);
        UpdateCodexStartState(context, item);

        PopulateLinks(item, linkTypeField, linkHint, linksList, linkTargets);
    }

    private static void ApplyFilters(TuiContext context)
    {
        var filterField = context.FilterField!;
        var statusField = context.StatusField!;
        var listView = context.ListView!;
        var listItemLookup = context.ListItemLookup;
        var filteredItems = context.FilteredItems;

        var filterText = filterField.Text?.ToString() ?? string.Empty;
        var statusText = statusField.Text?.ToString() ?? string.Empty;
        statusText = statusText.Trim();
        filterText = filterText.Trim();

        var allItems = context.AllItems;
        var filtered = allItems
            .Where(item =>
                (string.IsNullOrWhiteSpace(statusText)
                    || string.Equals(statusText, "all", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Status, statusText, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(filterText)
                    || item.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    || item.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => GetStatusRank(item.Status))
            .ThenBy(item => GetPriorityRank(item.Priority))
            .ToList();

        filteredItems.Clear();
        filteredItems.AddRange(filtered);

        var useEmoji = context.Config.Tui.UseEmoji;
        listItemLookup.Clear();
        var rows = new List<string>();
        var grouped = filteredItems
            .GroupBy(item => item.Status?.ToLowerInvariant() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetStatusRank(group.Key));

        foreach (var group in grouped)
        {
            var localStatusLabel = FormatStatusLabel(group.Key, useEmoji);
            var localStatusText = string.IsNullOrWhiteSpace(group.Key) ? "unknown" : group.Key;
            rows.Add(useEmoji ? $"[{localStatusLabel}]" : $"[{localStatusLabel}] {localStatusText}");
            listItemLookup.Add(null);

            foreach (var item in group
                .OrderBy(entry => GetPriorityRank(entry.Priority))
                .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase))
            {
                var statusLabelItem = FormatStatusLabel(item.Status, useEmoji).PadRight(useEmoji ? 16 : 4);
                var priorityLabel = FormatPriorityLabel(item.Priority, useEmoji).PadRight(useEmoji ? 12 : 6);
                rows.Add($"{statusLabelItem} {priorityLabel} {item.Id} {item.Title}");
                listItemLookup.Add(item);
            }
        }

        listView.SetSource(rows);
        if (rows.Count == 0)
        {
            UpdateDetails(context, -1);
            return;
        }

        if (listView.SelectedItem < 0 || listView.SelectedItem >= listItemLookup.Count
            || listItemLookup[listView.SelectedItem] is null)
        {
            var firstIndex = listItemLookup.FindIndex(item => item is not null);
            listView.SelectedItem = firstIndex;
        }

        UpdateDetails(context, listView.SelectedItem);
    }

    private static void ReloadItems(TuiContext context)
    {
        context.AllItems = LoadItems(context.RepoRoot, context.Config);
        ApplyFilters(context);
    }

    private static void SelectItemById(TuiContext context, string id)
    {
        var listView = context.ListView!;
        var listItemLookup = context.ListItemLookup;
        for (var i = 0; i < listItemLookup.Count; i++)
        {
            if (listItemLookup[i] is { } entry && entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                listView.SelectedItem = i;
                UpdateDetails(context, i);
                return;
            }
        }
    }

    private static int AddFieldRow(ScrollView view, string label, TextField field, int y, int labelWidth, int fieldWidth)
    {
        view.Add(new Label(label) { X = 1, Y = y, Width = labelWidth });
        field.X = labelWidth + 2;
        field.Y = y;
        field.Width = fieldWidth;
        view.Add(field);
        return y + 1;
    }

    private static int AddFieldRowWithPicker(ScrollView view, string label, TextField field, Button pickButton, int y, int labelWidth, int fieldWidth)
    {
        view.Add(new Label(label) { X = 1, Y = y, Width = labelWidth });
        field.X = labelWidth + 2;
        field.Y = y;
        field.Width = fieldWidth;
        pickButton.X = Pos.Right(field) + 1;
        pickButton.Y = y;
        view.Add(field);
        view.Add(pickButton);
        return y + 1;
    }

    private static string ResolveEnvValueFromSources(List<string> envLines, string fileKey, IReadOnlyList<string> envKeys, out string status)
    {
        if (TryGetEnvValue(envLines, fileKey, out var fileValue))
        {
            if (string.IsNullOrWhiteSpace(fileValue))
            {
                status = $"credentials.env entry for {fileKey} is empty (overrides env).";
                return string.Empty;
            }

            status = $"Found in credentials.env ({fileKey}).";
            return fileValue ?? string.Empty;
        }

        foreach (var envKey in envKeys)
        {
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                status = $"Found in environment ({envKey}).";
                return envValue;
            }
        }

        status = $"Missing ({fileKey}).";
        return string.Empty;
    }

    private static void LoadSettingsFields(TuiContext context)
    {
        var configReady = EnsureSettingsFile(context.ConfigPath, "config.json", context.DefaultConfigJson + "\n");
        string? configError = null;
        var loadedConfig = configReady
            ? WorkbenchConfig.Load(context.RepoRoot, out configError)
            : WorkbenchConfig.Default;
        if (configReady && !string.IsNullOrWhiteSpace(configError))
        {
            ShowInfo($"Config load error: {configError}");
        }

        var pathsConfig = loadedConfig.Paths ?? new PathsConfig();
        var idsConfig = loadedConfig.Ids ?? new IdsConfig();
        var prefixesConfig = idsConfig.Prefixes ?? new PrefixesConfig();
        var gitConfig = loadedConfig.Git ?? new GitConfig();
        var githubConfig = loadedConfig.Github ?? new GithubConfig();
        var validationConfig = loadedConfig.Validation ?? new ValidationConfig();
        var tuiConfig = loadedConfig.Tui ?? new TuiConfig();

        context.DocsRootField!.Text = pathsConfig.DocsRoot ?? string.Empty;
        context.WorkRootField!.Text = pathsConfig.WorkRoot ?? string.Empty;
        context.ItemsDirField!.Text = pathsConfig.ItemsDir ?? string.Empty;
        context.DoneDirField!.Text = pathsConfig.DoneDir ?? string.Empty;
        context.TemplatesDirField!.Text = pathsConfig.TemplatesDir ?? string.Empty;
        context.WorkboardFileField!.Text = pathsConfig.WorkboardFile ?? string.Empty;
        context.ThemeField!.Text = (tuiConfig.Theme ?? "powershell").Trim();
        context.UseEmojiCheck!.Checked = tuiConfig.UseEmoji;
        context.AutoRefreshSecondsField!.Text = tuiConfig.AutoRefreshSeconds.ToString(CultureInfo.InvariantCulture);

        context.IdWidthField!.Text = idsConfig.Width.ToString(CultureInfo.InvariantCulture);
        context.BugPrefixField!.Text = prefixesConfig.Bug ?? string.Empty;
        context.TaskPrefixField!.Text = prefixesConfig.Task ?? string.Empty;
        context.SpikePrefixField!.Text = prefixesConfig.Spike ?? string.Empty;

        context.GitBranchPatternField!.Text = gitConfig.BranchPattern ?? string.Empty;
        context.GitCommitPatternField!.Text = gitConfig.CommitMessagePattern ?? string.Empty;
        context.GitBaseBranchField!.Text = gitConfig.DefaultBaseBranch ?? string.Empty;
        context.GitRequireCleanCheck!.Checked = gitConfig.RequireCleanWorkingTree;

        context.GithubProviderField!.Text = githubConfig.Provider ?? string.Empty;
        context.GithubDefaultDraftCheck!.Checked = githubConfig.DefaultDraft;
        context.GithubHostField!.Text = githubConfig.Host ?? string.Empty;
        context.GithubOwnerField!.Text = githubConfig.Owner ?? string.Empty;
        context.GithubRepoField!.Text = githubConfig.Repository ?? string.Empty;

        context.LinkExcludeField!.Text = string.Join(", ", validationConfig.LinkExclude ?? new List<string>());
        context.DocExcludeField!.Text = string.Join(", ", validationConfig.DocExclude ?? new List<string>());

        if (EnsureSettingsFile(context.CredentialsPath, "credentials.env", string.Empty))
        {
            var envLines = File.Exists(context.CredentialsPath)
                ? File.ReadAllLines(context.CredentialsPath).ToList()
                : new List<string>();
            var providerValue = GetEnvValue(envLines, "WORKBENCH_AI_PROVIDER")
                ?? Environment.GetEnvironmentVariable("WORKBENCH_AI_PROVIDER")
                ?? "openai";
            context.AiProviderField!.Text = providerValue;

            var openAiStatus = string.Empty;
            var openAiKeyValue = ResolveEnvValueFromSources(
                envLines,
                "WORKBENCH_AI_OPENAI_KEY",
                new[] { "WORKBENCH_AI_OPENAI_KEY", "OPENAI_API_KEY" },
                out openAiStatus);
            context.AiOpenAiKeyField!.Text = openAiKeyValue;
            context.AiOpenAiKeyStatusLabel!.Text = $"Status: {openAiStatus}";

            context.AiModelField!.Text = GetEnvValue(envLines, "WORKBENCH_AI_MODEL")
                ?? Environment.GetEnvironmentVariable("WORKBENCH_AI_MODEL")
                ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
                ?? string.Empty;

            var githubStatus = string.Empty;
            var githubTokenValue = ResolveEnvValueFromSources(
                envLines,
                "WORKBENCH_GITHUB_TOKEN",
                new[] { "WORKBENCH_GITHUB_TOKEN", "GITHUB_TOKEN", "GH_TOKEN" },
                out githubStatus);
            context.GithubTokenField!.Text = githubTokenValue;
            context.GithubTokenStatusLabel!.Text = $"Status: {githubStatus}";
        }

        context.SettingsLoaded = true;
    }

    private static void ApplyCredentialEnvironment(TuiContext context)
    {
        void SetEnv(string key, string? value)
        {
            var normalized = value?.ToString();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                Environment.SetEnvironmentVariable(key, null);
            }
            else
            {
                Environment.SetEnvironmentVariable(key, normalized);
            }
        }

        SetEnv("WORKBENCH_AI_PROVIDER", context.AiProviderField!.Text?.ToString());
        SetEnv("WORKBENCH_AI_OPENAI_KEY", context.AiOpenAiKeyField!.Text?.ToString());
        SetEnv("WORKBENCH_AI_MODEL", context.AiModelField!.Text?.ToString());
        SetEnv("WORKBENCH_GITHUB_TOKEN", context.GithubTokenField!.Text?.ToString());
    }

    private static void SaveConfigFromFields(TuiContext context)
    {
        if (!EnsureSettingsFile(context.ConfigPath, "config.json", context.DefaultConfigJson + "\n"))
        {
            return;
        }

        if (!int.TryParse(context.IdWidthField!.Text?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var width))
        {
            ShowInfo("ID width must be a number.");
            return;
        }
        if (!int.TryParse(context.AutoRefreshSecondsField!.Text?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var autoRefreshSeconds)
            || autoRefreshSeconds < 0)
        {
            ShowInfo("Auto refresh seconds must be zero or greater.");
            return;
        }

        var updated = new WorkbenchConfig(
            new PathsConfig
            {
                DocsRoot = context.DocsRootField!.Text?.ToString() ?? string.Empty,
                WorkRoot = context.WorkRootField!.Text?.ToString() ?? string.Empty,
                ItemsDir = context.ItemsDirField!.Text?.ToString() ?? string.Empty,
                DoneDir = context.DoneDirField!.Text?.ToString() ?? string.Empty,
                TemplatesDir = context.TemplatesDirField!.Text?.ToString() ?? string.Empty,
                WorkboardFile = context.WorkboardFileField!.Text?.ToString() ?? string.Empty
            },
            new IdsConfig
            {
                Width = width,
                Prefixes = new PrefixesConfig
                {
                    Bug = context.BugPrefixField!.Text?.ToString() ?? string.Empty,
                    Task = context.TaskPrefixField!.Text?.ToString() ?? string.Empty,
                    Spike = context.SpikePrefixField!.Text?.ToString() ?? string.Empty
                }
            },
            new GitConfig
            {
                BranchPattern = context.GitBranchPatternField!.Text?.ToString() ?? string.Empty,
                CommitMessagePattern = context.GitCommitPatternField!.Text?.ToString() ?? string.Empty,
                DefaultBaseBranch = context.GitBaseBranchField!.Text?.ToString() ?? string.Empty,
                RequireCleanWorkingTree = context.GitRequireCleanCheck!.Checked
            },
            new GithubConfig
            {
                Provider = context.GithubProviderField!.Text?.ToString() ?? string.Empty,
                DefaultDraft = context.GithubDefaultDraftCheck!.Checked,
                Host = context.GithubHostField!.Text?.ToString() ?? string.Empty,
                Owner = context.GithubOwnerField!.Text?.ToString(),
                Repository = context.GithubRepoField!.Text?.ToString()
            },
            new ValidationConfig(
                ParseList(context.LinkExcludeField!.Text?.ToString()),
                ParseList(context.DocExcludeField!.Text?.ToString())),
            new TuiConfig
            {
                Theme = context.ThemeField!.Text?.ToString() ?? "powershell",
                UseEmoji = context.UseEmojiCheck!.Checked,
                AutoRefreshSeconds = autoRefreshSeconds
            });

        try
        {
            ConfigService.SaveConfig(context.RepoRoot, updated);
            context.Config = updated;
            ReloadItems(context);
            context.DocsAll = LoadDocs(context.RepoRoot, context.Config);
            ApplyDocsFilter(context);
            ApplyTheme(context, context.Config.Tui.Theme);
            ConfigureAutoRefresh(context, () => RefreshAll(context, showInfo: false));
            ShowInfo("Config saved.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void SaveCredentialsFromFields(TuiContext context)
    {
        if (!EnsureSettingsFile(context.CredentialsPath, "credentials.env", string.Empty))
        {
            return;
        }

        var envLines = File.Exists(context.CredentialsPath)
            ? File.ReadAllLines(context.CredentialsPath).ToList()
            : new List<string>();
        SetEnvValue(envLines, "WORKBENCH_AI_PROVIDER", context.AiProviderField!.Text?.ToString());
        SetEnvValue(envLines, "WORKBENCH_AI_OPENAI_KEY", context.AiOpenAiKeyField!.Text?.ToString());
        SetEnvValue(envLines, "WORKBENCH_AI_MODEL", context.AiModelField!.Text?.ToString());
        SetEnvValue(envLines, "WORKBENCH_GITHUB_TOKEN", context.GithubTokenField!.Text?.ToString());

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(context.CredentialsPath) ?? context.RepoRoot);
            File.WriteAllText(context.CredentialsPath, string.Join("\n", envLines) + "\n");
            ApplyCredentialEnvironment(context);
            var openAiStatus = string.Empty;
            ResolveEnvValueFromSources(
                envLines,
                "WORKBENCH_AI_OPENAI_KEY",
                new[] { "WORKBENCH_AI_OPENAI_KEY", "OPENAI_API_KEY" },
                out openAiStatus);
            context.AiOpenAiKeyStatusLabel!.Text = $"Status: {openAiStatus}";

            var githubStatus = string.Empty;
            ResolveEnvValueFromSources(
                envLines,
                "WORKBENCH_GITHUB_TOKEN",
                new[] { "WORKBENCH_GITHUB_TOKEN", "GITHUB_TOKEN", "GH_TOKEN" },
                out githubStatus);
            context.GithubTokenStatusLabel!.Text = $"Status: {githubStatus}";
            ShowInfo("Credentials saved.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void ApplyDocsFilter(TuiContext context)
    {
        var filter = context.DocsFilterField!.Text?.ToString() ?? string.Empty;
        filter = filter.Trim();
        var filtered = context.DocsAll
            .Where(path => string.IsNullOrWhiteSpace(filter)
                || path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        context.DocsTree!.ClearObjects();
        var roots = BuildDocsTree(filtered);
        context.DocsTree.AddObjects(roots);
    }

    private static void ActivateSelectedDoc(TuiContext context)
    {
        var docsTree = context.DocsTree!;
        if (docsTree.SelectedObject is not TreeNode node || node.Tag is not string path)
        {
            return;
        }

        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var resolved = ResolveDocsLink(context.RepoRoot, context.Config, path);
        SetCommandPreview(context, $"open \"{resolved}\"");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = resolved,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void ExecutePull(TuiContext context)
    {
        try
        {
            SetCommandPreview(context, "git pull --ff-only");
            var result = GitService.Run(context.RepoRoot, "pull", "--ff-only");
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git pull failed.");
            }
            UpdateGitInfo(context);
            ShowInfo("Pull completed.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void ExecuteStage(TuiContext context)
    {
        try
        {
            SetCommandPreview(context, "git add -A");
            var result = GitService.Run(context.RepoRoot, "add", "-A");
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git add failed.");
            }
            UpdateGitInfo(context);
            ShowInfo("Staged all changes.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void ExecutePush(TuiContext context)
    {
        try
        {
            var branch = GitService.GetCurrentBranch(context.RepoRoot);
            SetCommandPreview(context, $"git push -u origin {branch}");
            GitService.Push(context.RepoRoot, branch);
            UpdateGitInfo(context);
            ShowInfo("Push completed.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void RefreshAll(TuiContext context, bool showInfo)
    {
        var selectedId = GetSelectedItem(context)?.Id;
        var loadedConfig = WorkbenchConfig.Load(context.RepoRoot, out var configError);
        if (!string.IsNullOrWhiteSpace(configError) && showInfo)
        {
            ShowInfo($"Config load error: {configError}");
        }

        context.Config = loadedConfig;
        ReloadItems(context);
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            SelectItemById(context, selectedId);
        }

        context.DocsAll = LoadDocs(context.RepoRoot, context.Config);
        ApplyDocsFilter(context);

        context.SettingsLoaded = false;
        if (context.TabView?.SelectedTab == context.SettingsTab)
        {
            LoadSettingsFields(context);
        }

        ApplyTheme(context, context.Config.Tui.Theme);
        UpdateGitInfo(context);
        UpdateCodexStartState(context, GetSelectedItem(context));
        ConfigureAutoRefresh(context, () => RefreshAll(context, showInfo: false));
        UpdateLastRefreshLabel(context);
    }

    private static void ApplyTheme(TuiContext context, string? themeName)
    {
        var top = context.Top!;
        var window = context.Window!;
        var tabView = context.TabView!;
        var navFrame = context.NavFrame!;
        var detailsFrame = context.DetailsFrame!;
        var detailsBody = context.DetailsBody!;
        var footer = context.Footer!;
        var listView = context.ListView!;
        var docsTree = context.DocsTree!;
        var docsPreviewHeader = context.DocsPreviewHeader!;
        var docsPreview = context.DocsPreview!;
        var settingsScroll = context.SettingsScroll!;
        var inputScheme = context.InputScheme!;
        var defaultScheme = context.DefaultScheme;

        var normalized = (themeName ?? "powershell").Trim().ToLowerInvariant();
        if (string.Equals(normalized, "default", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "dark";
        }
        ColorScheme scheme;

        ColorScheme BuildScheme(
            Color normalFg,
            Color normalBg,
            Color focusFg,
            Color focusBg,
            Color hotNormalFg,
            Color hotNormalBg,
            Color hotFocusFg,
            Color hotFocusBg,
            Color disabledFg,
            Color disabledBg)
        {
            return new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(normalFg, normalBg),
                Focus = Application.Driver.MakeAttribute(focusFg, focusBg),
                HotNormal = Application.Driver.MakeAttribute(hotNormalFg, hotNormalBg),
                HotFocus = Application.Driver.MakeAttribute(hotFocusFg, hotFocusBg),
                Disabled = Application.Driver.MakeAttribute(disabledFg, disabledBg)
            };
        }

        ColorScheme BuildReadOnlyScheme(ColorScheme source)
        {
            return new ColorScheme
            {
                Normal = source.Normal,
                Focus = source.Normal,
                HotNormal = source.HotNormal,
                HotFocus = source.HotNormal,
                Disabled = source.Disabled
            };
        }

        switch (normalized)
        {
            case "powershell":
                scheme = BuildScheme(
                    Color.Gray, Color.Blue,
                    Color.Blue, Color.Gray,
                    Color.Cyan, Color.Blue,
                    Color.BrightYellow, Color.Gray,
                    Color.Gray, Color.Blue);
                break;
            case "dark":
                scheme = BuildScheme(
                    Color.White, Color.Black,
                    Color.Black, Color.White,
                    Color.Cyan, Color.Black,
                    Color.Black, Color.Cyan,
                    Color.Gray, Color.Black);
                break;
            case "light":
                scheme = BuildScheme(
                    Color.Black, Color.White,
                    Color.White, Color.Black,
                    Color.Blue, Color.White,
                    Color.White, Color.Blue,
                    Color.Gray, Color.White);
                break;
            case "solarized-dark":
                scheme = BuildScheme(
                    Color.Gray, Color.Black,
                    Color.Black, Color.Gray,
                    Color.Blue, Color.Black,
                    Color.BrightYellow, Color.Gray,
                    Color.Gray, Color.Black);
                break;
            case "solarized-light":
                scheme = BuildScheme(
                    Color.Gray, Color.White,
                    Color.White, Color.Gray,
                    Color.Blue, Color.White,
                    Color.BrightYellow, Color.Gray,
                    Color.Gray, Color.White);
                break;
            case "nord":
                scheme = BuildScheme(
                    Color.Gray, Color.Black,
                    Color.Black, Color.Cyan,
                    Color.Blue, Color.Black,
                    Color.Red, Color.Cyan,
                    Color.Gray, Color.Black);
                break;
            case "gruvbox":
                scheme = BuildScheme(
                    Color.BrightYellow, Color.Black,
                    Color.Black, Color.BrightYellow,
                    Color.Cyan, Color.Black,
                    Color.Red, Color.BrightYellow,
                    Color.Gray, Color.Black);
                break;
            case "monokai":
                scheme = BuildScheme(
                    Color.White, Color.Black,
                    Color.Black, Color.Green,
                    Color.Cyan, Color.Black,
                    Color.BrightYellow, Color.Green,
                    Color.Gray, Color.Black);
                break;
            case "high-contrast":
                scheme = BuildScheme(
                    Color.White, Color.Black,
                    Color.Black, Color.BrightYellow,
                    Color.Cyan, Color.Black,
                    Color.Red, Color.BrightYellow,
                    Color.Gray, Color.Black);
                break;
            default:
                scheme = defaultScheme ?? Colors.Base;
                break;
        }

        inputScheme.Normal = scheme.Normal;
        inputScheme.Focus = scheme.Focus;

        top.ColorScheme = scheme;
        window.ColorScheme = scheme;
        tabView.ColorScheme = scheme;
        navFrame.ColorScheme = scheme;
        detailsFrame.ColorScheme = scheme;
        detailsBody.ColorScheme = BuildReadOnlyScheme(detailsFrame.ColorScheme);
        footer.ColorScheme = scheme;
        listView.ColorScheme = scheme;
        docsTree.ColorScheme = scheme;
        docsPreviewHeader.ColorScheme = detailsFrame.ColorScheme;
        docsPreview.ColorScheme = detailsFrame.ColorScheme;
        settingsScroll.ColorScheme = scheme;
        if (context.StatusBar is not null)
        {
            context.StatusBar.ColorScheme = scheme;
        }
    }
}
