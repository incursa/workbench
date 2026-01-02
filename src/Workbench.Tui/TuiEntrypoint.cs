using Terminal.Gui.Graphs;
using Workbench.Core;
using Workbench.Core.Voice;
using Workbench.VoiceViz;

namespace Workbench.Tui;

using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Terminal.Gui;
using Terminal.Gui.Trees;
using Workbench;
using Workbench.Tui.VoiceViz;

public static partial class TuiEntrypoint
{
    public static async Task<int> RunAsync(string[] args)
    {
        var repoRoot = Repository.FindRepoRoot(Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            await Console.Error.WriteLineAsync("Not a git repository.").ConfigureAwait(false);
            return 2;
        }

        EnvLoader.LoadRepoEnv(repoRoot);
        var config = WorkbenchConfig.Load(repoRoot, out _);
        var allItems = LoadItems(repoRoot, config);
        StatusBar? statusBar = null;
        ColorScheme? defaultScheme = null;
        var workItemStatusOptions = new[] { "all", "draft", "ready", "in-progress", "blocked", "done", "dropped" };
        var workItemTypeOptions = new[] { "task", "bug", "spike" };
        var docTypeOptions = new[] { "spec", "adr", "doc", "runbook", "guide" };
        var context = new TuiContext(repoRoot, config, allItems, workItemStatusOptions, workItemTypeOptions, docTypeOptions)
        {
            StatusBar = statusBar,
            DefaultScheme = defaultScheme
        };
        context.CodexAvailable = CodexService.TryGetVersion(repoRoot, out var codexVersion, out var codexError);
        context.CodexVersion = codexVersion;
        context.CodexError = codexError;
        var filteredItems = context.FilteredItems;
        var listItemLookup = context.ListItemLookup;
        var linkTargets = context.LinkTargets;

        Application.Init();
        try
        {
            var top = Application.Top;
            var inputScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Black, Color.White),
                Focus = Application.Driver.MakeAttribute(Color.Black, Color.White)
            };
            context.InputScheme = inputScheme;
            var window = new Window("Workbench TUI")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var tabView = new TabView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(1)
            };

            var navFrame = new FrameView("Work Items")
            {
                X = 0,
                Y = 0,
                Width = Dim.Percent(50),
                Height = Dim.Fill()
            };

            var detailsFrame = new FrameView("Details")
            {
                X = Pos.Right(navFrame),
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var footer = new View
            {
                X = 0,
                Y = Pos.Bottom(tabView),
                Width = Dim.Fill(),
                Height = 1
            };

            var filterLabel = new Label("Filter:")
            {
                X = 1,
                Y = 0
            };

            var filterField = new TextField(string.Empty)
            {
                X = Pos.Right(filterLabel) + 1,
                Y = 0,
                Width = Dim.Fill(1)
            };

            var statusLabel = new Label("Status:")
            {
                X = 1,
                Y = 1
            };

            var statusField = new TextField("all")
            {
                X = Pos.Right(statusLabel) + 1,
                Y = 1,
                Width = Dim.Fill(9)
            };

            var statusPickButton = CreatePickerButton(statusField, workItemStatusOptions, "Status filter");
            statusPickButton.X = Pos.Right(statusField) + 1;
            statusPickButton.Y = 1;

            var listView = new ListView(new List<string>())
            {
                X = 0,
                Y = 2,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var detailsHeader = new Label("Select a work item to see details.")
            {
                X = 1,
                Y = 0,
                Width = Dim.Fill(),
                Height = 8
            };
            var startWorkButton = new Button("Start work")
            {
                X = 1,
                Y = Pos.Bottom(detailsHeader)
            };
            startWorkButton.Enabled = false;

            var completeWorkButton = new Button("Complete work")
            {
                X = Pos.Right(startWorkButton) + 2,
                Y = Pos.Bottom(detailsHeader)
            };
            completeWorkButton.Enabled = false;

            var codexWorkButton = new Button("Start work (Codex)")
            {
                X = 1,
                Y = Pos.Bottom(startWorkButton) + 1
            };
            codexWorkButton.Enabled = false;

            var codexHintLabel = new Label(string.Empty)
            {
                X = Pos.Right(codexWorkButton) + 2,
                Y = Pos.Bottom(startWorkButton) + 1,
                Width = Dim.Fill()
            };

            var linkTypeLabel = new Label("Links:")
            {
                X = 1,
                Y = Pos.Bottom(codexWorkButton)
            };

            var linksList = new ListView(new List<string>())
            {
                X = 0,
                Y = Pos.Bottom(linkTypeLabel) + 1,
                Width = Dim.Fill(),
                Height = 6
            };

            var linkTypeField = new TextField("all")
            {
                X = Pos.Right(linkTypeLabel) + 1,
                Y = Pos.Bottom(completeWorkButton),
                Width = 12
            };

            var linkHint = new Label(string.Empty)
            {
                X = Pos.Right(linkTypeField) + 2,
                Y = Pos.Bottom(completeWorkButton),
                Width = Dim.Fill()
            };

            var detailsDivider = new LineView(Orientation.Horizontal)
            {
                X = 1,
                Y = Pos.Bottom(linksList),
                Width = Dim.Fill(2),
                Height = 1
            };

            var detailsBody = new TextView
            {
                X = 1,
                Y = Pos.Bottom(detailsDivider) + 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = true
            };
            detailsBody.ColorScheme = detailsFrame.ColorScheme;

            context.DryRunEnabled = false;
            var dryRunLabel = new Label("Dry-run: OFF")
            {
                X = 1,
                Y = 0
            };

            var commandPreviewLabel = new Label("Command: (none)")
            {
                X = 16,
                Y = 0
            };
            var gitInfoLabel = new Label("Git: (unknown)")
            {
                X = Pos.AnchorEnd(30),
                Y = 0,
                Width = 29
            };
            context.DryRunLabel = dryRunLabel;
            context.CommandPreviewLabel = commandPreviewLabel;
            context.FilterField = filterField;
            context.StatusField = statusField;
            context.ListView = listView;
            context.DetailsHeader = detailsHeader;
            context.LinksList = linksList;
            context.LinkTypeField = linkTypeField;
            context.LinkHint = linkHint;

            void UpdateGitInfo()
            {
                try
                {
                    var branch = GitService.GetCurrentBranch(repoRoot);
                    var clean = GitService.IsClean(repoRoot);
                    gitInfoLabel.Text = $"Git: {branch} {(clean ? "clean" : "dirty")}";
                }
                catch
                {
                    gitInfoLabel.Text = "Git: unavailable";
#pragma warning disable ERP022
                }
#pragma warning restore ERP022
            }

            void ShowDocPreviewDialog(string path, string resolvedPath, string content)
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

            bool TryShowPreviewForLink(string link)
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

                var resolved = ResolveLink(repoRoot, trimmedLink);
                if (!File.Exists(resolved))
                {
                    ShowInfo("Doc not found.");
                    return true;
                }

                try
                {
                    var content = File.ReadAllText(resolved);
                    SetCommandPreview(context, $"preview \"{resolved}\"");
                    ShowDocPreviewDialog(trimmedLink, resolved, content);
                    return true;
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    return true;
                }
            }


            WorkItem? GetSelectedItem()
            {
                if (listView.SelectedItem < 0 || listView.SelectedItem >= listItemLookup.Count)
                {
                    return null;
                }

                return listItemLookup[listView.SelectedItem];
            }

            string ApplyPattern(string pattern, WorkItem item)
            {
                return pattern
                    .Replace("{id}", item.Id, StringComparison.Ordinal)
                    .Replace("{slug}", item.Slug, StringComparison.Ordinal)
                    .Replace("{title}", item.Title, StringComparison.Ordinal);
            }

            int FindNextItemIndex(int startIndex)
            {
                for (var i = startIndex + 1; i < listItemLookup.Count; i++)
                {
                    if (listItemLookup[i] is not null)
                    {
                        return i;
                    }
                }

                for (var i = startIndex - 1; i >= 0; i--)
                {
                    if (listItemLookup[i] is not null)
                    {
                        return i;
                    }
                }

                return -1;
            }

            void UpdateCodexStartState(WorkItem? item)
            {
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
                    if (!GitService.IsClean(repoRoot))
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

            void UpdateDetails(int index)
            {
                if (index < 0 || index >= listItemLookup.Count)
                {
                    detailsHeader.Text = "Select a work item to see details.";
                    detailsBody.Text = string.Empty;
                    linkTargets.Clear();
                    linksList!.SetSource(new List<string>());
                    linkHint!.Text = string.Empty;
                    SetCommandPreview(context, "(none)");
                    startWorkButton.Enabled = false;
                    completeWorkButton.Enabled = false;
                    UpdateCodexStartState(null);
                    return;
                }

                var item = listItemLookup[index];
                if (item is null)
                {
                    detailsHeader.Text = "Select a work item to see details.";
                    detailsBody.Text = string.Empty;
                    linkTargets.Clear();
                    linksList!.SetSource(new List<string>());
                    linkHint!.Text = string.Empty;
                    SetCommandPreview(context, "(none)");
                    startWorkButton.Enabled = false;
                    completeWorkButton.Enabled = false;
                    UpdateCodexStartState(null);
                    return;
                }
                var specsCount = item.Related.Specs.Count;
                var adrsCount = item.Related.Adrs.Count;
                var filesCount = item.Related.Files.Count;
                var issuesCount = item.Related.Issues.Count;
                var prsCount = item.Related.Prs.Count;

                detailsHeader.Text = $"[{item.Status}] {item.Id}\n{item.Title}\n\nOwner: {item.Owner ?? "?"}\nPriority: {item.Priority ?? "?"}\nUpdated: {item.Updated ?? "?"}\n\nLinked: specs {specsCount}, adrs {adrsCount}, files {filesCount}\nIssues: {issuesCount}, PRs: {prsCount}\n\nEnter: open selected link";
                detailsBody.Text = item.Body;
                SetCommandPreview(context, $"workbench item show {item.Id}");
                startWorkButton.Enabled = true;
                completeWorkButton.Enabled = !string.Equals(item.Status, "done", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(item.Status, "dropped", StringComparison.OrdinalIgnoreCase);
                UpdateCodexStartState(item);

                PopulateLinks(item, linkTypeField!, linkHint!, linksList!, linkTargets);
            }

            void ApplyFilters()
            {
                var filterText = filterField.Text?.ToString() ?? string.Empty;
                var statusText = statusField.Text?.ToString() ?? string.Empty;
                statusText = statusText.Trim();
                filterText = filterText.Trim();

                filteredItems = allItems
                    .Where(item =>
                        (string.IsNullOrWhiteSpace(statusText)
                            || string.Equals(statusText, "all", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(item.Status, statusText, StringComparison.OrdinalIgnoreCase))
                        && (string.IsNullOrWhiteSpace(filterText)
                            || item.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                            || item.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(item => GetStatusRank(item.Status))
                    .ThenBy(item => GetPriorityRank(item.Priority))
                    .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var rows = new List<string>();
                listItemLookup.Clear();
                var useEmoji = config.Tui.UseEmoji;
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
                    UpdateDetails(-1);
                    return;
                }

                if (listView.SelectedItem < 0 || listView.SelectedItem >= listItemLookup.Count || listItemLookup[listView.SelectedItem] is null)
                {
                    var firstIndex = listItemLookup.FindIndex(item => item is not null);
                    listView.SelectedItem = firstIndex;
                }

                UpdateDetails(listView.SelectedItem);
            }

            void ReloadItems()
            {
                allItems = LoadItems(repoRoot, config);
                context.AllItems = allItems;
                ApplyFilters();
            }

            void SelectItemById(string id)
            {
                for (var i = 0; i < listItemLookup.Count; i++)
                {
                    if (listItemLookup[i] is { } entry && entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                    {
                        listView.SelectedItem = i;
                        UpdateDetails(i);
                        return;
                    }
                }
            }

            var docsAll = LoadDocs(repoRoot, config);
            context.DocsAll = docsAll;
            var docsFilterLabel = new Label("Filter:")
            {
                X = 1,
                Y = 0
            };

            var docsFilterField = new TextField(string.Empty)
            {
                X = Pos.Right(docsFilterLabel) + 1,
                Y = 0,
                Width = Dim.Fill(1)
            };

            var docsTree = new TreeView<ITreeNode>
            {
                X = 0,
                Y = 1,
                Width = 40,
                Height = Dim.Fill(),
                TreeBuilder = new TreeNodeBuilder()
            };

            var docsPreview = new TextView
            {
                X = Pos.Right(docsTree),
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = true
            };

            var docsPreviewHeader = new Label("Preview")
            {
                X = Pos.Right(docsTree),
                Y = 0,
                Width = Dim.Fill()
            };
            context.DocsFilterField = docsFilterField;
            context.DocsTree = docsTree;
            context.DocsPreview = docsPreview;
            context.DocsPreviewHeader = docsPreviewHeader;

            var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
            var credentialsPath = Path.Combine(repoRoot, ".workbench", "credentials.env");
            var defaultConfigJson = JsonSerializer.Serialize(
                WorkbenchConfig.Default,
                WorkbenchJsonContext.Default.WorkbenchConfig);
            context.SettingsLoaded = false;
            var githubProviderOptions = new[] { "octokit", "gh" };
            var aiProviderOptions = new[] { "openai", "none" };
            var themeOptions = new[]
            {
                "powershell",
                "dark",
                "light",
                "solarized-dark",
                "solarized-light",
                "nord",
                "gruvbox",
                "monokai",
                "high-contrast"
            };
            var settingsScroll = new ScrollView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                ShowVerticalScrollIndicator = true
            };
            context.SettingsScroll = settingsScroll;
            context.ConfigPath = configPath;
            context.CredentialsPath = credentialsPath;
            context.DefaultConfigJson = defaultConfigJson;
            context.ThemeOptions = themeOptions;
            context.GithubProviderOptions = githubProviderOptions;
            context.AiProviderOptions = aiProviderOptions;

            var docsRootField = new TextField(string.Empty);
            var workRootField = new TextField(string.Empty);
            var itemsDirField = new TextField(string.Empty);
            var doneDirField = new TextField(string.Empty);
            var templatesDirField = new TextField(string.Empty);
            var workboardFileField = new TextField(string.Empty);
            var themeField = new TextField("default");
            var themePickButton = CreatePickerButton(themeField, themeOptions, "Theme");
            var useEmojiCheck = new CheckBox("Use emoji labels");
            context.DocsRootField = docsRootField;
            context.WorkRootField = workRootField;
            context.ItemsDirField = itemsDirField;
            context.DoneDirField = doneDirField;
            context.TemplatesDirField = templatesDirField;
            context.WorkboardFileField = workboardFileField;
            context.ThemeField = themeField;
            context.ThemePickButton = themePickButton;
            context.UseEmojiCheck = useEmojiCheck;

            var idWidthField = new TextField(string.Empty);
            var bugPrefixField = new TextField(string.Empty);
            var taskPrefixField = new TextField(string.Empty);
            var spikePrefixField = new TextField(string.Empty);
            context.IdWidthField = idWidthField;
            context.BugPrefixField = bugPrefixField;
            context.TaskPrefixField = taskPrefixField;
            context.SpikePrefixField = spikePrefixField;

            var gitBranchPatternField = new TextField(string.Empty);
            var gitCommitPatternField = new TextField(string.Empty);
            var gitBaseBranchField = new TextField(string.Empty);
            var gitRequireCleanCheck = new CheckBox("Require clean working tree");
            context.GitBranchPatternField = gitBranchPatternField;
            context.GitCommitPatternField = gitCommitPatternField;
            context.GitBaseBranchField = gitBaseBranchField;
            context.GitRequireCleanCheck = gitRequireCleanCheck;

            var githubProviderField = new TextField(string.Empty);
            var githubProviderPickButton = CreatePickerButton(githubProviderField, githubProviderOptions, "GitHub provider");
            var githubDefaultDraftCheck = new CheckBox("Default draft PRs");
            var githubHostField = new TextField(string.Empty);
            var githubOwnerField = new TextField(string.Empty);
            var githubRepoField = new TextField(string.Empty);
            context.GithubProviderField = githubProviderField;
            context.GithubProviderPickButton = githubProviderPickButton;
            context.GithubDefaultDraftCheck = githubDefaultDraftCheck;
            context.GithubHostField = githubHostField;
            context.GithubOwnerField = githubOwnerField;
            context.GithubRepoField = githubRepoField;

            var linkExcludeField = new TextField(string.Empty);
            var docExcludeField = new TextField(string.Empty);
            context.LinkExcludeField = linkExcludeField;
            context.DocExcludeField = docExcludeField;

            var aiProviderField = new TextField("openai");
            var aiProviderPickButton = CreatePickerButton(aiProviderField, aiProviderOptions, "AI provider");
            var aiOpenAiKeyField = new TextField(string.Empty) { Secret = true };
            var aiOpenAiKeyStatusLabel = new Label(string.Empty);
            var aiModelField = new TextField(string.Empty);
            var githubTokenField = new TextField(string.Empty) { Secret = true };
            var githubTokenStatusLabel = new Label(string.Empty);
            context.AiProviderField = aiProviderField;
            context.AiProviderPickButton = aiProviderPickButton;
            context.AiOpenAiKeyField = aiOpenAiKeyField;
            context.AiOpenAiKeyStatusLabel = aiOpenAiKeyStatusLabel;
            context.AiModelField = aiModelField;
            context.GithubTokenField = githubTokenField;
            context.GithubTokenStatusLabel = githubTokenStatusLabel;

            int AddFieldRow(ScrollView view, string label, TextField field, int y, int labelWidth, int fieldWidth)
            {
                view.Add(new Label(label) { X = 1, Y = y, Width = labelWidth });
                field.X = labelWidth + 2;
                field.Y = y;
                field.Width = fieldWidth;
                view.Add(field);
                return y + 1;
            }

            int AddFieldRowWithPicker(ScrollView view, string label, TextField field, Button pickButton, int y, int labelWidth, int fieldWidth)
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

            string ResolveEnvValueFromSources(List<string> envLines, string fileKey, IReadOnlyList<string> envKeys, out string status)
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

            void LoadSettingsFields()
            {
                var configReady = EnsureSettingsFile(configPath, "config.json", defaultConfigJson + "\n");
                string? configError = null;
                var loadedConfig = configReady
                    ? WorkbenchConfig.Load(repoRoot, out configError)
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

                docsRootField.Text = pathsConfig.DocsRoot ?? string.Empty;
                workRootField.Text = pathsConfig.WorkRoot ?? string.Empty;
                itemsDirField.Text = pathsConfig.ItemsDir ?? string.Empty;
                doneDirField.Text = pathsConfig.DoneDir ?? string.Empty;
                templatesDirField.Text = pathsConfig.TemplatesDir ?? string.Empty;
                workboardFileField.Text = pathsConfig.WorkboardFile ?? string.Empty;
                themeField.Text = (loadedConfig.Tui?.Theme ?? "powershell").Trim();
                useEmojiCheck.Checked = loadedConfig.Tui?.UseEmoji ?? true;

                idWidthField.Text = idsConfig.Width.ToString(CultureInfo.InvariantCulture);
                bugPrefixField.Text = prefixesConfig.Bug ?? string.Empty;
                taskPrefixField.Text = prefixesConfig.Task ?? string.Empty;
                spikePrefixField.Text = prefixesConfig.Spike ?? string.Empty;

                gitBranchPatternField.Text = gitConfig.BranchPattern ?? string.Empty;
                gitCommitPatternField.Text = gitConfig.CommitMessagePattern ?? string.Empty;
                gitBaseBranchField.Text = gitConfig.DefaultBaseBranch ?? string.Empty;
                gitRequireCleanCheck.Checked = gitConfig.RequireCleanWorkingTree;

                githubProviderField.Text = githubConfig.Provider ?? string.Empty;
                githubDefaultDraftCheck.Checked = githubConfig.DefaultDraft;
                githubHostField.Text = githubConfig.Host ?? string.Empty;
                githubOwnerField.Text = githubConfig.Owner ?? string.Empty;
                githubRepoField.Text = githubConfig.Repository ?? string.Empty;

                linkExcludeField.Text = string.Join(", ", validationConfig.LinkExclude ?? new List<string>());
                docExcludeField.Text = string.Join(", ", validationConfig.DocExclude ?? new List<string>());

                if (EnsureSettingsFile(credentialsPath, "credentials.env", string.Empty))
                {
                    var envLines = File.Exists(credentialsPath)
                        ? File.ReadAllLines(credentialsPath).ToList()
                        : new List<string>();
                    var providerValue = GetEnvValue(envLines, "WORKBENCH_AI_PROVIDER")
                        ?? Environment.GetEnvironmentVariable("WORKBENCH_AI_PROVIDER")
                        ?? "openai";
                    aiProviderField.Text = providerValue;

                    var openAiStatus = string.Empty;
                    var openAiKeyValue = ResolveEnvValueFromSources(
                        envLines,
                        "WORKBENCH_AI_OPENAI_KEY",
                        new[] { "WORKBENCH_AI_OPENAI_KEY", "OPENAI_API_KEY" },
                        out openAiStatus);
                    aiOpenAiKeyField.Text = openAiKeyValue;
                    aiOpenAiKeyStatusLabel.Text = $"Status: {openAiStatus}";

                    aiModelField.Text = GetEnvValue(envLines, "WORKBENCH_AI_MODEL")
                        ?? Environment.GetEnvironmentVariable("WORKBENCH_AI_MODEL")
                        ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
                        ?? string.Empty;

                    var githubStatus = string.Empty;
                    var githubTokenValue = ResolveEnvValueFromSources(
                        envLines,
                        "WORKBENCH_GITHUB_TOKEN",
                        new[] { "WORKBENCH_GITHUB_TOKEN", "GITHUB_TOKEN", "GH_TOKEN" },
                        out githubStatus);
                    githubTokenField.Text = githubTokenValue;
                    githubTokenStatusLabel.Text = $"Status: {githubStatus}";
                }

                context.SettingsLoaded = true;
            }

            void ApplyCredentialEnvironment()
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

                SetEnv("WORKBENCH_AI_PROVIDER", aiProviderField.Text?.ToString());
                SetEnv("WORKBENCH_AI_OPENAI_KEY", aiOpenAiKeyField.Text?.ToString());
                SetEnv("WORKBENCH_AI_MODEL", aiModelField.Text?.ToString());
                SetEnv("WORKBENCH_GITHUB_TOKEN", githubTokenField.Text?.ToString());
            }

            void SaveConfigFromFields()
            {
                if (!EnsureSettingsFile(configPath, "config.json", defaultConfigJson + "\n"))
                {
                    return;
                }

                if (!int.TryParse(idWidthField.Text?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var width))
                {
                    ShowInfo("ID width must be a number.");
                    return;
                }

                var updated = new WorkbenchConfig(
                    new PathsConfig
                    {
                        DocsRoot = docsRootField.Text?.ToString() ?? string.Empty,
                        WorkRoot = workRootField.Text?.ToString() ?? string.Empty,
                        ItemsDir = itemsDirField.Text?.ToString() ?? string.Empty,
                        DoneDir = doneDirField.Text?.ToString() ?? string.Empty,
                        TemplatesDir = templatesDirField.Text?.ToString() ?? string.Empty,
                        WorkboardFile = workboardFileField.Text?.ToString() ?? string.Empty
                    },
                    new IdsConfig
                    {
                        Width = width,
                        Prefixes = new PrefixesConfig
                        {
                            Bug = bugPrefixField.Text?.ToString() ?? string.Empty,
                            Task = taskPrefixField.Text?.ToString() ?? string.Empty,
                            Spike = spikePrefixField.Text?.ToString() ?? string.Empty
                        }
                    },
                    new GitConfig
                    {
                        BranchPattern = gitBranchPatternField.Text?.ToString() ?? string.Empty,
                        CommitMessagePattern = gitCommitPatternField.Text?.ToString() ?? string.Empty,
                        DefaultBaseBranch = gitBaseBranchField.Text?.ToString() ?? string.Empty,
                        RequireCleanWorkingTree = gitRequireCleanCheck.Checked
                    },
                    new GithubConfig
                    {
                        Provider = githubProviderField.Text?.ToString() ?? string.Empty,
                        DefaultDraft = githubDefaultDraftCheck.Checked,
                        Host = githubHostField.Text?.ToString() ?? string.Empty,
                        Owner = githubOwnerField.Text?.ToString(),
                        Repository = githubRepoField.Text?.ToString()
                    },
                    new ValidationConfig(
                        ParseList(linkExcludeField.Text?.ToString()),
                        ParseList(docExcludeField.Text?.ToString())),
                    new TuiConfig
                    {
                        Theme = themeField.Text?.ToString() ?? "powershell",
                        UseEmoji = useEmojiCheck.Checked
                    });

                try
                {
                    ConfigService.SaveConfig(repoRoot, updated);
                    config = updated;
                    context.Config = updated;
                    ReloadItems();
                    docsAll = LoadDocs(repoRoot, config);
                    context.DocsAll = docsAll;
                    context.DocsAll = docsAll;
                    context.DocsAll = docsAll;
                    ApplyDocsFilter();
                    ApplyTheme(config.Tui.Theme);
                    ShowInfo("Config saved.");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void SaveCredentialsFromFields()
            {
                if (!EnsureSettingsFile(credentialsPath, "credentials.env", string.Empty))
                {
                    return;
                }

                var envLines = File.Exists(credentialsPath)
                    ? File.ReadAllLines(credentialsPath).ToList()
                    : new List<string>();
                SetEnvValue(envLines, "WORKBENCH_AI_PROVIDER", aiProviderField.Text?.ToString());
                SetEnvValue(envLines, "WORKBENCH_AI_OPENAI_KEY", aiOpenAiKeyField.Text?.ToString());
                SetEnvValue(envLines, "WORKBENCH_AI_MODEL", aiModelField.Text?.ToString());
                SetEnvValue(envLines, "WORKBENCH_GITHUB_TOKEN", githubTokenField.Text?.ToString());

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(credentialsPath) ?? repoRoot);
                    File.WriteAllText(credentialsPath, string.Join("\n", envLines) + "\n");
                    ApplyCredentialEnvironment();
                    var openAiStatus = string.Empty;
                    ResolveEnvValueFromSources(
                        envLines,
                        "WORKBENCH_AI_OPENAI_KEY",
                        new[] { "WORKBENCH_AI_OPENAI_KEY", "OPENAI_API_KEY" },
                        out openAiStatus);
                    aiOpenAiKeyStatusLabel.Text = $"Status: {openAiStatus}";

                    var githubStatus = string.Empty;
                    ResolveEnvValueFromSources(
                        envLines,
                        "WORKBENCH_GITHUB_TOKEN",
                        new[] { "WORKBENCH_GITHUB_TOKEN", "GITHUB_TOKEN", "GH_TOKEN" },
                        out githubStatus);
                    githubTokenStatusLabel.Text = $"Status: {githubStatus}";
                    ShowInfo("Credentials saved.");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            var labelWidth = 22;
            var fieldWidth = 50;
            var settingsY = 0;

            settingsScroll.Add(new Label("Config") { X = 1, Y = settingsY });
            settingsY++;
            settingsScroll.Add(new Label($"Path: {configPath}") { X = 1, Y = settingsY, Width = Dim.Fill() });
            settingsY++;
            settingsY = AddFieldRow(settingsScroll, "Docs root:", docsRootField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Work root:", workRootField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Items dir:", itemsDirField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Done dir:", doneDirField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Templates dir:", templatesDirField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Workboard file:", workboardFileField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRowWithPicker(settingsScroll, "Theme:", themeField, themePickButton, settingsY, labelWidth, 14);
            useEmojiCheck.X = 1;
            useEmojiCheck.Y = settingsY;
            settingsScroll.Add(useEmojiCheck);
            settingsY++;
            settingsY++;
            settingsY = AddFieldRow(settingsScroll, "ID width:", idWidthField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Bug prefix:", bugPrefixField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Task prefix:", taskPrefixField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Spike prefix:", spikePrefixField, settingsY, labelWidth, fieldWidth);
            settingsY++;
            settingsY = AddFieldRow(settingsScroll, "Branch pattern:", gitBranchPatternField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Commit pattern:", gitCommitPatternField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Base branch:", gitBaseBranchField, settingsY, labelWidth, fieldWidth);
            gitRequireCleanCheck.X = 1;
            gitRequireCleanCheck.Y = settingsY;
            settingsScroll.Add(gitRequireCleanCheck);
            settingsY++;
            settingsY = AddFieldRowWithPicker(settingsScroll, "GitHub provider:", githubProviderField, githubProviderPickButton, settingsY, labelWidth, 14);
            githubDefaultDraftCheck.X = 1;
            githubDefaultDraftCheck.Y = settingsY;
            settingsScroll.Add(githubDefaultDraftCheck);
            settingsY++;
            settingsY = AddFieldRow(settingsScroll, "GitHub host:", githubHostField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "GitHub owner:", githubOwnerField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "GitHub repo:", githubRepoField, settingsY, labelWidth, fieldWidth);
            settingsY++;
            settingsY = AddFieldRow(settingsScroll, "Link exclude:", linkExcludeField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "Doc exclude:", docExcludeField, settingsY, labelWidth, fieldWidth);
            settingsScroll.Add(new Label("Comma-separated values.") { X = labelWidth + 2, Y = settingsY, Width = Dim.Fill() });
            settingsY++;

            var saveConfigButton = new Button("Save config")
            {
                X = 1,
                Y = settingsY
            };
            var reloadConfigButton = new Button("Reload config")
            {
                X = Pos.Right(saveConfigButton) + 2,
                Y = settingsY
            };
            saveConfigButton.Clicked += () => SaveConfigFromFields();
            reloadConfigButton.Clicked += () =>
            {
                context.SettingsLoaded = false;
                LoadSettingsFields();
            };
            settingsScroll.Add(saveConfigButton, reloadConfigButton);
            settingsY += 2;

            settingsScroll.Add(new Label("Credentials") { X = 1, Y = settingsY });
            settingsY++;
            settingsScroll.Add(new Label($"Path: {credentialsPath}") { X = 1, Y = settingsY, Width = Dim.Fill() });
            settingsY++;
            settingsY = AddFieldRowWithPicker(settingsScroll, "AI provider:", aiProviderField, aiProviderPickButton, settingsY, labelWidth, 14);
            settingsY = AddFieldRow(settingsScroll, "AI OpenAI key:", aiOpenAiKeyField, settingsY, labelWidth, fieldWidth);
            aiOpenAiKeyStatusLabel.X = labelWidth + 2;
            aiOpenAiKeyStatusLabel.Y = settingsY;
            aiOpenAiKeyStatusLabel.Width = Dim.Fill();
            settingsScroll.Add(aiOpenAiKeyStatusLabel);
            settingsY++;
            settingsY = AddFieldRow(settingsScroll, "AI model:", aiModelField, settingsY, labelWidth, fieldWidth);
            settingsY = AddFieldRow(settingsScroll, "GitHub token:", githubTokenField, settingsY, labelWidth, fieldWidth);
            githubTokenStatusLabel.X = labelWidth + 2;
            githubTokenStatusLabel.Y = settingsY;
            githubTokenStatusLabel.Width = Dim.Fill();
            settingsScroll.Add(githubTokenStatusLabel);
            settingsY++;

            var saveCredsButton = new Button("Save credentials")
            {
                X = 1,
                Y = settingsY
            };
            var reloadCredsButton = new Button("Reload credentials")
            {
                X = Pos.Right(saveCredsButton) + 2,
                Y = settingsY
            };
            saveCredsButton.Clicked += () => SaveCredentialsFromFields();
            reloadCredsButton.Clicked += () =>
            {
                context.SettingsLoaded = false;
                LoadSettingsFields();
            };
            settingsScroll.Add(saveCredsButton, reloadCredsButton);
            settingsY += 2;

            settingsScroll.ContentSize = new Size(100, settingsY + 1);

            void ApplyDocsFilter()
            {
                var filter = docsFilterField.Text?.ToString() ?? string.Empty;
                filter = filter.Trim();
                var filtered = docsAll
                    .Where(path => string.IsNullOrWhiteSpace(filter)
                        || path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                docsTree.ClearObjects();
                var roots = BuildDocsTree(filtered);
                docsTree.AddObjects(roots);
            }

            static string NormalizeIssueCloseTarget(string issue)
            {
                var trimmed = issue.Trim();
                if (trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal) && trimmed.Length > 1)
                {
                    trimmed = trimmed[1..^1].Trim();
                }

                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal))
                {
                    var linkStart = trimmed.IndexOf("](", StringComparison.Ordinal);
                    if (linkStart > 0 && linkStart + 2 < trimmed.Length - 1)
                    {
                        trimmed = trimmed[(linkStart + 2)..^1].Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    return string.Empty;
                }

                if (trimmed.All(char.IsDigit))
                {
                    return $"#{trimmed}";
                }

                return trimmed;
            }

            static List<string> BuildClosingLines(WorkItem item)
            {
                var lines = new List<string>();
                foreach (var issue in item.Related.Issues)
                {
                    var target = NormalizeIssueCloseTarget(issue);
                    if (string.IsNullOrWhiteSpace(target))
                    {
                        continue;
                    }
                    lines.Add($"Closes {target}");
                }
                return lines;
            }

            static string BuildCodexPrompt(WorkItem item)
            {
                var tags = item.Tags.Count > 0 ? string.Join(", ", item.Tags) : "(none)";
                var priority = string.IsNullOrWhiteSpace(item.Priority) ? "-" : item.Priority;
                var owner = string.IsNullOrWhiteSpace(item.Owner) ? "-" : item.Owner;

                return $"""
                    Work item context:
                    Id: {item.Id}
                    Title: {item.Title}
                    Type: {item.Type}
                    Status: {item.Status}
                    Priority: {priority}
                    Owner: {owner}
                    Tags: {tags}
                    Body:
                    {item.Body}

                    Implement the work item in this repository.
                    """;
            }

            void StartCodexWork()
            {
                var item = GetSelectedItem();
                if (item is null)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                if (!context.CodexAvailable)
                {
                    ShowInfo("Codex CLI is not available.");
                    return;
                }

                try
                {
                    if (!GitService.IsClean(repoRoot))
                    {
                        ShowInfo("Working tree is not clean.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    return;
                }

                try
                {
                    var startBranch = ApplyPattern(context.Config.Git.BranchPattern, item);
                    if (GitService.BranchExists(repoRoot, startBranch))
                    {
                        var choice = MessageBox.Query(
                            "Branch exists",
                            $"Branch '{startBranch}' already exists. Checkout it?",
                            "Checkout",
                            "Cancel");
                        if (choice != 0)
                        {
                            return;
                        }

                        var checkout = GitService.Run(repoRoot, "checkout", startBranch);
                        if (checkout.ExitCode != 0)
                        {
                            throw new InvalidOperationException(checkout.StdErr.Length > 0 ? checkout.StdErr : "git checkout failed.");
                        }
                    }
                    else
                    {
                        GitService.CheckoutNewBranch(repoRoot, startBranch);
                    }

                    GitService.Push(repoRoot, startBranch);

                    var prompt = BuildCodexPrompt(item);
                    SetCommandPreview(context, "codex --full-auto --web-search --prompt <work item>");
                    CodexService.StartFullAutoInTerminal(repoRoot, prompt);
                    UpdateGitInfo();
                    UpdateDetails(listView.SelectedItem);
                    ShowInfo($"{item.Id} started in Codex on {startBranch}.");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void ShowStartWorkDialog()
            {
                var item = GetSelectedItem();
                if (item is null)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                if (context.Config.Git.RequireCleanWorkingTree && !GitService.IsClean(repoRoot))
                {
                    ShowInfo("Working tree is not clean.");
                    return;
                }

                var branch = ApplyPattern(context.Config.Git.BranchPattern, item);
                var pushCheck = new CheckBox("Push branch to origin") { X = 1, Y = 1, Checked = false };
                var prCheck = new CheckBox("Create pull request") { X = 1, Y = 2, Checked = false };
                var draftCheck = new CheckBox("Draft PR") { X = 4, Y = 3, Checked = context.Config.Github.DefaultDraft };
                var baseBranchLabel = new Label("Base branch (optional):") { X = 1, Y = 4 };
                var baseBranchField = new TextField(string.Empty) { X = 26, Y = 4, Width = 20 };
                var previewLabel = new Label("Command: (none)") { X = 1, Y = 6, Width = Dim.Fill(2) };

                var dialog = new Dialog("Start work", 70, 14)
                {
                    ColorScheme = Colors.Dialog
                };
                dialog.Add(
                    new Label($"Item: {item.Id} {item.Title}") { X = 1, Y = 0 },
                    pushCheck,
                    prCheck,
                    draftCheck,
                    baseBranchLabel,
                    baseBranchField,
                    previewLabel);

                void UpdatePreview()
                {
                    var command = $"start work {item.Id} (branch {branch})";
                    if (pushCheck.Checked)
                    {
                        command += " + push";
                    }
                    if (prCheck.Checked)
                    {
                        command += " + pr";
                    }
                    if (prCheck.Checked && draftCheck.Checked)
                    {
                        command += " (draft)";
                    }
                    var baseBranch = baseBranchField.Text?.ToString();
                    if (prCheck.Checked && !string.IsNullOrWhiteSpace(baseBranch))
                    {
                        command += $" base {baseBranch}";
                    }
                    if (context.DryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(context, command);
                }

                pushCheck.Toggled += _ => UpdatePreview();
                prCheck.Toggled += _ => UpdatePreview();
                draftCheck.Toggled += _ => UpdatePreview();
                baseBranchField.TextChanged += _ => UpdatePreview();
                UpdatePreview();

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var startButton = new Button("Start");
                cancelButton.Clicked += () => Application.RequestStop();
                startButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(startButton);

                Application.Run(dialog);
                if (!confirmed)
                {
                    return;
                }

                if (context.DryRunEnabled)
                {
                    ShowInfo("Dry-run enabled; no files were changed.");
                    return;
                }

                var shouldPush = pushCheck.Checked || prCheck.Checked;
                var baseBranch = baseBranchField.Text?.ToString();
                var useDraft = draftCheck.Checked;

                try
                {
                    var startBranch = ApplyPattern(context.Config.Git.BranchPattern, item);
                    if (GitService.BranchExists(repoRoot, startBranch))
                    {
                        var choice = MessageBox.Query(
                            "Branch exists",
                            $"Branch '{startBranch}' already exists. Checkout it?",
                            "Checkout",
                            "Cancel");
                        if (choice != 0)
                        {
                            return;
                        }

                        var checkout = GitService.Run(repoRoot, "checkout", startBranch);
                        if (checkout.ExitCode != 0)
                        {
                            throw new InvalidOperationException(checkout.StdErr.Length > 0 ? checkout.StdErr : "git checkout failed.");
                        }
                    }
                    else
                    {
                        GitService.CheckoutNewBranch(repoRoot, startBranch);
                    }

                    var updated = WorkItemService.UpdateStatus(item.Path, "in-progress", note: null);
                    GitService.Add(repoRoot, updated.Path);
                    var commitMessage = $"Start {updated.Id}: {updated.Title}";
                    GitService.Commit(repoRoot, commitMessage);

                    if (shouldPush)
                    {
                        GitService.Push(repoRoot, startBranch);
                    }

                    string? prUrl = null;
                    if (prCheck.Checked)
                    {
                        var prTitle = $"{updated.Id}: {updated.Title}";
                        var prBody = PullRequestBuilder.BuildBody(updated);
                        var prRepo = GithubService.ResolveRepo(repoRoot, context.Config);
                        var baseTarget = string.IsNullOrWhiteSpace(baseBranch) ? context.Config.Git.DefaultBaseBranch : baseBranch;
                        prUrl = RunWithBusyDialog(
                            "Creating PR",
                            "Creating pull request...",
                            () => GithubService.CreatePullRequestAsync(repoRoot, context.Config, prRepo, prTitle, prBody, baseTarget, useDraft));
                        if (!string.IsNullOrWhiteSpace(prUrl))
                        {
                            WorkItemService.AddPrLink(updated.Path, prUrl);
                        }
                    }

                    ReloadItems();
                    SelectItemById(updated.Id);

                    var summary = $"{updated.Id} started on {startBranch}.";
                    if (!string.IsNullOrWhiteSpace(prUrl))
                    {
                        summary += $" PR: {prUrl}";
                    }
                    ShowInfo(summary);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            startWorkButton.Clicked += ShowStartWorkDialog;
            codexWorkButton.Clicked += StartCodexWork;

            void ShowCompleteWorkDialog()
            {
                var item = GetSelectedItem();
                if (item is null)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                if (context.Config.Git.RequireCleanWorkingTree && !GitService.IsClean(repoRoot))
                {
                    ShowInfo("Working tree is not clean.");
                    return;
                }

                var hasIssues = item.Related.Issues.Count > 0;
                var pushCheck = new CheckBox("Push branch to origin") { X = 1, Y = 1, Checked = true };
                var prCheck = new CheckBox(hasIssues ? "Create pull request (closes issues)" : "Create pull request")
                {
                    X = 1,
                    Y = 2,
                    Checked = hasIssues
                };
                var draftCheck = new CheckBox("Draft PR") { X = 4, Y = 3, Checked = context.Config.Github.DefaultDraft };
                var baseBranchLabel = new Label("Base branch (optional):") { X = 1, Y = 4 };
                var baseBranchField = new TextField(string.Empty) { X = 26, Y = 4, Width = 20 };
                var previewLabel = new Label("Command: (none)") { X = 1, Y = 6, Width = Dim.Fill(2) };

                var dialog = new Dialog("Complete work", 70, 14)
                {
                    ColorScheme = Colors.Dialog
                };
                dialog.Add(
                    new Label($"Item: {item.Id} {item.Title}") { X = 1, Y = 0 },
                    pushCheck,
                    prCheck,
                    draftCheck,
                    baseBranchLabel,
                    baseBranchField,
                    previewLabel);

                void UpdatePreview()
                {
                    var command = $"complete work {item.Id}";
                    if (pushCheck.Checked)
                    {
                        command += " + push";
                    }
                    if (prCheck.Checked)
                    {
                        command += " + pr";
                    }
                    if (prCheck.Checked && draftCheck.Checked)
                    {
                        command += " (draft)";
                    }
                    var baseBranch = baseBranchField.Text?.ToString();
                    if (prCheck.Checked && !string.IsNullOrWhiteSpace(baseBranch))
                    {
                        command += $" base {baseBranch}";
                    }
                    if (context.DryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(context, command);
                }

                pushCheck.Toggled += _ => UpdatePreview();
                prCheck.Toggled += _ => UpdatePreview();
                draftCheck.Toggled += _ => UpdatePreview();
                baseBranchField.TextChanged += _ => UpdatePreview();
                UpdatePreview();

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var completeButton = new Button("Complete");
                cancelButton.Clicked += () => Application.RequestStop();
                completeButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(completeButton);

                Application.Run(dialog);
                if (!confirmed)
                {
                    return;
                }

                if (context.DryRunEnabled)
                {
                    ShowInfo("Dry-run enabled; no files were changed.");
                    return;
                }

                var shouldPush = pushCheck.Checked || prCheck.Checked;
                var baseBranch = baseBranchField.Text?.ToString();
                var useDraft = draftCheck.Checked;

                try
                {
                    var updated = WorkItemService.UpdateStatus(item.Path, "done", note: null);
                    GitService.Add(repoRoot, updated.Path);
                    var commitMessage = $"Complete {updated.Id}: {updated.Title}";
                    GitService.Commit(repoRoot, commitMessage);

                    if (shouldPush)
                    {
                        var currentBranch = GitService.GetCurrentBranch(repoRoot);
                        GitService.Push(repoRoot, currentBranch);
                    }

                    string? prUrl = null;
                    if (prCheck.Checked)
                    {
                        var prTitle = $"{updated.Id}: {updated.Title}";
                        var prBody = PullRequestBuilder.BuildBody(updated);
                        var closingLines = BuildClosingLines(updated);
                        if (closingLines.Count > 0)
                        {
                            var bodyLines = new List<string>();
                            if (!string.IsNullOrWhiteSpace(prBody))
                            {
                                bodyLines.Add(prBody.TrimEnd());
                                bodyLines.Add(string.Empty);
                            }
                            bodyLines.Add("## Issues");
                            bodyLines.AddRange(closingLines);
                            prBody = string.Join("\n", bodyLines).TrimEnd();
                        }
                        var prRepo = GithubService.ResolveRepo(repoRoot, context.Config);
                        var baseTarget = string.IsNullOrWhiteSpace(baseBranch) ? context.Config.Git.DefaultBaseBranch : baseBranch;
                        prUrl = RunWithBusyDialog(
                            "Creating PR",
                            "Creating pull request...",
                            () => GithubService.CreatePullRequestAsync(repoRoot, context.Config, prRepo, prTitle, prBody, baseTarget, useDraft));
                        if (!string.IsNullOrWhiteSpace(prUrl))
                        {
                            WorkItemService.AddPrLink(updated.Path, prUrl);
                        }
                    }

                    ReloadItems();
                    SelectItemById(updated.Id);

                    var summary = $"{updated.Id} completed.";
                    if (!string.IsNullOrWhiteSpace(prUrl))
                    {
                        summary += $" PR: {prUrl}";
                    }
                    ShowInfo(summary);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            completeWorkButton.Clicked += ShowCompleteWorkDialog;

            void ActivateSelectedDoc()
            {
                docsTree.ActivateSelectedObjectIfAny();
            }

            void ShowSyncDialog()
            {
                var includeDone = false;
                var syncIssues = true;
                var force = false;
                var workboard = true;

                var summaryLabel = new Label("Sync nav updates doc backlinks, indexes, and workboard.")
                {
                    X = 1,
                    Y = 0
                };
                var includeDoneCheck = new CheckBox("Include done items") { X = 1, Y = 1, Checked = includeDone };
                var syncIssuesCheck = new CheckBox("Sync issue links") { X = 1, Y = 2, Checked = syncIssues };
                var forceCheck = new CheckBox("Force index rewrite") { X = 1, Y = 3, Checked = force };
                var workboardCheck = new CheckBox("Regenerate workboard") { X = 1, Y = 4, Checked = workboard };
                var previewLabel = new Label("Command: (none)") { X = 1, Y = 6, Width = Dim.Fill(2) };

                var dialog = new Dialog("Sync navigation", 70, 14);
                dialog.Add(summaryLabel, includeDoneCheck, syncIssuesCheck, forceCheck, workboardCheck, previewLabel);

                void UpdatePreview()
                {
                    var command = "workbench nav sync";
                    if (includeDoneCheck.Checked)
                    {
                        command += " --include-done";
                    }
                    if (!syncIssuesCheck.Checked)
                    {
                        command += " --issues false";
                    }
                    if (forceCheck.Checked)
                    {
                        command += " --force";
                    }
                    if (!workboardCheck.Checked)
                    {
                        command += " --workboard false";
                    }
                    if (context.DryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(context, command);
                }

                includeDoneCheck.Toggled += _ => UpdatePreview();
                syncIssuesCheck.Toggled += _ => UpdatePreview();
                forceCheck.Toggled += _ => UpdatePreview();
                workboardCheck.Toggled += _ => UpdatePreview();
                UpdatePreview();

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var runButton = new Button("Run");
                cancelButton.Clicked += () => Application.RequestStop();
                runButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(runButton);

                Application.Run(dialog);
                if (!confirmed)
                {
                    return;
                }

                includeDone = includeDoneCheck.Checked;
                syncIssues = syncIssuesCheck.Checked;
                force = forceCheck.Checked;
                workboard = workboardCheck.Checked;

                _ = RunSyncWithOptionsAsync(includeDone, syncIssues, force, workboard);
            }

            async Task RunSyncWithOptionsAsync(bool includeDone, bool syncIssues, bool force, bool workboard)
            {
                var command = "workbench nav sync";
                if (includeDone)
                {
                    command += " --include-done";
                }
                if (!syncIssues)
                {
                    command += " --issues false";
                }
                if (force)
                {
                    command += " --force";
                }
                if (!workboard)
                {
                    command += " --workboard false";
                }
                if (context.DryRunEnabled)
                {
                    command += " --dry-run";
                }
                SetCommandPreview(context, command);

                try
                {
                    var result = await NavigationService.SyncNavigationAsync(
                            repoRoot,
                            config,
                            includeDone,
                            syncIssues,
                            force,
                            workboard,
                            context.DryRunEnabled)
                        .ConfigureAwait(false);
                    var summary = $"Docs: {result.DocsUpdated}, Items: {result.ItemsUpdated}, Index: {result.IndexFilesUpdated}, Workboard: {result.WorkboardUpdated}";
                    if (context.DryRunEnabled)
                    {
                        summary = $"Dry-run: {summary}";
                    }
                    Application.MainLoop.Invoke(() => ShowInfo(summary));
                }
                catch (Exception ex)
                {
                    Application.MainLoop.Invoke(() => ShowError(ex));
                }
            }

            void ShowValidateDialog()
            {
                var summaryLabel = new Label("Validate repo checks items, docs, and links.")
                {
                    X = 1,
                    Y = 0
                };
                var skipDocSchemaCheck = new CheckBox("Skip doc schema") { X = 1, Y = 1, Checked = false };
                var includeLabel = new Label("Link include (comma-separated):") { X = 1, Y = 3 };
                var includeField = new TextField(string.Empty) { X = 1, Y = 4, Width = Dim.Fill(2) };
                var excludeLabel = new Label("Link exclude (comma-separated):") { X = 1, Y = 6 };
                var excludeField = new TextField(string.Empty) { X = 1, Y = 7, Width = Dim.Fill(2) };
                var previewLabel = new Label("Command: (none)") { X = 1, Y = 9, Width = Dim.Fill(2) };

                var dialog = new Dialog("Validate repo", 76, 16);
                dialog.Add(summaryLabel, skipDocSchemaCheck, includeLabel, includeField, excludeLabel, excludeField, previewLabel);

                void UpdatePreview()
                {
                    var command = "workbench validate";
                    if (skipDocSchemaCheck.Checked)
                    {
                        command += " --skip-doc-schema";
                    }
                    if (!string.IsNullOrWhiteSpace(includeField.Text?.ToString()))
                    {
                        command += " --link-include <...>";
                    }
                    if (!string.IsNullOrWhiteSpace(excludeField.Text?.ToString()))
                    {
                        command += " --link-exclude <...>";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(context, command);
                }

                skipDocSchemaCheck.Toggled += _ => UpdatePreview();
                includeField.TextChanged += _ => UpdatePreview();
                excludeField.TextChanged += _ => UpdatePreview();
                UpdatePreview();

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var runButton = new Button("Run");
                cancelButton.Clicked += () => Application.RequestStop();
                runButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(runButton);

                Application.Run(dialog);
                if (!confirmed)
                {
                    return;
                }

                var includes = ParseList(includeField.Text?.ToString());
                var excludes = ParseList(excludeField.Text?.ToString());
                var options = new ValidationOptions(includes, excludes, skipDocSchemaCheck.Checked);

                RunValidateWithOptions(options);
            }

            void RunValidateWithOptions(ValidationOptions options)
            {
                try
                {
                    var result = ValidationService.ValidateRepo(repoRoot, config, options);
                    var summary = $"Errors: {result.Errors.Count}, Warnings: {result.Warnings.Count}, Items: {result.WorkItemCount}, Markdown: {result.MarkdownFileCount}";
                    ShowInfo(summary);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void ToggleDryRun()
            {
                context.DryRunEnabled = !context.DryRunEnabled;
                dryRunLabel.Text = context.DryRunEnabled ? "Dry-run: ON" : "Dry-run: OFF";
                UpdateDetails(listView.SelectedItem);
            }

            void ShowVoiceWorkItemDialog()
            {
                var typeField = new TextField(string.Empty);
                var typePickButton = CreatePickerButton(typeField, workItemTypeOptions, "Work item type");
                var instructions = "Say: title and details.\nIf you didn't pick a type, say: type (task/bug/spike).\nPress Stop to finish or Cancel to discard.";

                var recording = CaptureRecordingDialog(
                    "Voice work item",
                    instructions,
                    startRow =>
                    {
                        var typeLabel = new Label("Type (optional):") { X = 1, Y = startRow };
                        typeField.X = Pos.Right(typeLabel) + 1;
                        typeField.Y = startRow;
                        typeField.Width = 12;
                        typePickButton.X = Pos.Right(typeField) + 1;
                        typePickButton.Y = startRow;
                        return new View[] { typeLabel, typeField, typePickButton };
                    });

                if (recording is null || recording.WasCanceled)
                {
                    return;
                }

                var typeOverride = typeField.Text?.ToString();
                RunVoiceWorkItemFromRecording(recording, string.IsNullOrWhiteSpace(typeOverride) ? null : typeOverride);
            }

            void ShowVoiceEditDialog()
            {
                var item = GetSelectedItem();
                if (item is null)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                var instructions = "Say the changes to apply.\nExamples: update summary, add acceptance criteria, adjust tags.\nPress Stop to finish or Cancel to discard.";

                var recording = CaptureRecordingDialog(
                    "Voice edit item",
                    instructions,
                    startRow =>
                    {
                        var itemLabel = new Label($"Item: {item.Id} {item.Title}")
                        {
                            X = 1,
                            Y = startRow,
                            Width = Dim.Fill(2)
                        };
                        return new View[] { itemLabel };
                    });

                if (recording is null || recording.WasCanceled)
                {
                    return;
                }

                RunVoiceEditWorkItemFromRecording(recording, item);
            }

            void ShowVoiceDocDialog()
            {
                var typeField = new TextField("spec");
                var typePickButton = CreatePickerButton(typeField, docTypeOptions, "Doc type");
                var instructions = "Say: title, summary, and key sections.\nExample: scope, decisions, and next steps.\nPress Stop to finish or Cancel to discard.";

                var recording = CaptureRecordingDialog(
                    "Voice doc",
                    instructions,
                    startRow =>
                    {
                        var typeLabel = new Label("Doc type:") { X = 1, Y = startRow };
                        typeField.X = Pos.Right(typeLabel) + 1;
                        typeField.Y = startRow;
                        typeField.Width = 12;
                        typePickButton.X = Pos.Right(typeField) + 1;
                        typePickButton.Y = startRow;
                        return new View[] { typeLabel, typeField, typePickButton };
                    });

                if (recording is null || recording.WasCanceled)
                {
                    return;
                }

                var type = typeField.Text?.ToString();
                if (string.IsNullOrWhiteSpace(type))
                {
                    ShowInfo("Doc type is required.");
                    CleanupTempFiles(recording.WavPaths);
                    return;
                }

                RunVoiceDocFromRecording(recording, type.Trim().ToLowerInvariant());
            }

            static string BuildWorkItemEditPrompt(WorkItem item, string transcript)
            {
                var tags = item.Tags.Count > 0 ? string.Join(", ", item.Tags) : "(none)";
                var priority = string.IsNullOrWhiteSpace(item.Priority) ? "-" : item.Priority;
                var owner = string.IsNullOrWhiteSpace(item.Owner) ? "-" : item.Owner;

                return $"""
                    Update the work item using the user's voice notes. Preserve anything not mentioned.
                    The user update describes changes to apply, not text to copy verbatim. Do not include
                    instructions like "update this task" unless the user explicitly wants that phrasing in
                    the work item content. Only update the title and body sections (summary and acceptance
                    criteria). Do not modify front matter fields, related links, or other metadata.
                    Existing work item:
                    Id: {item.Id}
                    Title: {item.Title}
                    Type: {item.Type}
                    Status: {item.Status}
                    Priority: {priority}
                    Owner: {owner}
                    Tags: {tags}
                    Body:
                    {item.Body}

                    User update:
                    {transcript}

                    Return a full JSON draft per your instructions.
                    """;
            }

            AudioRecordingResult? CaptureRecordingDialog(
                string title,
                string instructions,
                Func<int, View[]>? addInputs)
            {
                var voiceConfig = VoiceConfig.Load();
                var options = EqualizerOptions.Load();
                var model = new EqualizerModel(options.BandCount);
                var ringSize = Math.Max(options.FftSize * 4, 4096);
                var tap = new AudioTap(model, options, ringSize);
                SpectrumAnalyzer? analyzer = null;

                var instructionLines = Math.Max(1, instructions.Split('\n').Length);
                var extraRows = addInputs is null ? 0 : 1;
                var dialogHeight = 16 + instructionLines + extraRows;
                var dialog = new Dialog(title, 60, dialogHeight)
                {
                    ColorScheme = Colors.Dialog
                };
                var statusLabel = new Label("Starting recorder...")
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill(2)
                };
                var instructionsLabel = new Label(instructions)
                {
                    X = 1,
                    Y = 2,
                    Width = Dim.Fill(2),
                    Height = instructionLines
                };
                var inputsRow = 2 + instructionLines;
                if (addInputs is not null)
                {
                    var inputs = addInputs(inputsRow);
                    foreach (var view in inputs)
                    {
                        dialog.Add(view);
                    }
                    inputsRow += 1;
                }

                var equalizerView = new EqualizerView(model)
                {
                    X = 1,
                    Y = inputsRow + 1,
                    Width = Dim.Fill(2),
                    Height = 7,
                    ColorScheme = Colors.Dialog
                };

                dialog.Add(statusLabel, instructionsLabel, equalizerView);

                var stopButton = new Button("Stop") { Enabled = false };
                var cancelButton = new Button("Cancel");

                IAudioRecordingSession? session = null;
                var startCts = new CancellationTokenSource();

                if (options.EnableSpectrum && tap.RingBuffer is not null)
                {
                    analyzer = new SpectrumAnalyzer(model, tap.RingBuffer, options, voiceConfig.Format.SampleRateHz);
                    analyzer.Start();
                }

                var startTask = Task.Run(async () =>
                {
                    var limits = AudioLimiter.Calculate(
                        voiceConfig.Format,
                        voiceConfig.MaxDuration,
                        voiceConfig.MaxUploadBytes);
                    var recordingOptions = new AudioRecordingOptions(
                        voiceConfig.Format,
                        limits.MaxDuration,
                        limits.MaxBytes,
                        Path.GetTempPath(),
                        "workbench-voice",
                        FramesPerBuffer: 512,
                        Tap: tap);
                    var recorder = new PortAudioRecorder();
                    return await recorder.StartAsync(recordingOptions, startCts.Token).ConfigureAwait(false);
                });

                var refreshToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(50), _ =>
                {
                    equalizerView.SetNeedsDisplay();
                    return true;
                });

                dialog.KeyDown += args =>
                {
                    if (args.KeyEvent.Key == Key.Esc)
                    {
                        _ = StopRecordingAsync(dialog, session, startCts, cancel: true);
                        args.Handled = true;
                    }
                    else if (args.KeyEvent.Key == Key.Enter)
                    {
                        _ = StopRecordingAsync(dialog, session, startCts, cancel: false);
                        args.Handled = true;
                    }
                };

                stopButton.Clicked += () => _ = StopRecordingAsync(dialog, session, startCts, cancel: false);
                cancelButton.Clicked += () => _ = StopRecordingAsync(dialog, session, startCts, cancel: true);
                dialog.AddButton(stopButton);
                dialog.AddButton(cancelButton);

                _ = startTask.ContinueWith((task, _) =>
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        if (task.IsCanceled)
                        {
                            statusLabel.Text = "Recorder start canceled.";
                            stopButton.Enabled = false;
                            return;
                        }
                        if (task.IsFaulted)
                        {
                            var ex = task.Exception?.GetBaseException()
                                ?? new InvalidOperationException("Recorder start failed.");
                            Application.RequestStop(dialog);
                            ShowError(ex);
                            return;
                        }

#pragma warning disable RS0030
                        session = task.Result;
#pragma warning restore RS0030
                        statusLabel.Text = "Recording...";
                        stopButton.Enabled = true;
                    });
                }, null, TaskScheduler.Default);

                Application.Run(dialog);

                Application.MainLoop.RemoveTimeout(refreshToken);
                startCts.Cancel();
                startCts.Dispose();

                AudioRecordingResult? result = null;
                if (session is not null)
                {
                    if (!session.Completion.IsCompleted)
                    {
#pragma warning disable RS0030
                        session.CancelAsync(CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore RS0030
                    }

#pragma warning disable RS0030
                    result = session.Completion.GetAwaiter().GetResult();
#pragma warning restore RS0030

#pragma warning disable RS0030
                    session.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030
                }

#pragma warning disable RS0030
                analyzer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030

                return result;
            }

            static async Task StopRecordingAsync(Dialog dialog, IAudioRecordingSession? session, CancellationTokenSource? startCts, bool cancel)
            {
                try
                {
                    if (session is null)
                    {
                        if (startCts != null)
                        {
                            await startCts.CancelAsync().ConfigureAwait(false);
                        }

                        Application.MainLoop.Invoke(() => Application.RequestStop(dialog));
                        return;
                    }
                    if (cancel)
                    {
                        await session.CancelAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                    {
                        await session.StopAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // ignored
#pragma warning disable ERP022
                }
#pragma warning restore ERP022

                Application.MainLoop.Invoke(() => Application.RequestStop(dialog));
            }

            void RunVoiceWorkItemFromRecording(AudioRecordingResult recording, string? typeOverride)
            {
                var voiceConfig = VoiceConfig.Load();
                if (!OpenAiTranscriptionClient.TryCreate(out var transcriptionClient, out var reason))
                {
                    ShowInfo($"Transcription disabled: {reason}");
                    CleanupTempFiles(recording.WavPaths);
                    return;
                }

                string? transcript = null;
                try
                {
                    transcript = RunWithBusyDialog(
                        "Transcribing",
                        "Transcribing audio...",
                        () => TranscribeRecordingAsync(recording, voiceConfig, transcriptionClient!));
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    CleanupTempFiles(recording.WavPaths);
                    return;
                }

                try
                {
                    if (!AiWorkItemClient.TryCreate(out var client, out var failedReason))
                    {
                        ShowInfo($"AI work item generation disabled: {failedReason}");
                        CleanupTempFiles(recording.WavPaths);
                        return;
                    }

                    var draft = RunWithBusyDialog<WorkItemDraft?>(
                        "Generating",
                        "Generating work item...",
                        () => client!.GenerateDraftAsync(transcript));
                    if (draft == null || string.IsNullOrWhiteSpace(draft.Title))
                    {
                        ShowInfo("AI did not return a valid work item draft.");
                        CleanupTempFiles(recording.WavPaths);
                        return;
                    }

                    var type = ResolveWorkItemType(typeOverride, draft.Type);
                    var created = WorkItemService.CreateItem(repoRoot, config, type, draft.Title, status: null, priority: null, owner: null);
                    WorkItemService.ApplyDraft(created.Path, draft);

                    allItems = LoadItems(repoRoot, config);
                    context.AllItems = allItems;
                    filteredItems.Clear();
                    filteredItems.AddRange(allItems);
                    ApplyFilters();
                    ShowInfo($"{created.Id} created at {created.Path}");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
                finally
                {
                    CleanupTempFiles(recording.WavPaths);
                }
            }

            void RunVoiceEditWorkItemFromRecording(AudioRecordingResult recording, WorkItem item)
            {
                var voiceConfig = VoiceConfig.Load();
                if (!OpenAiTranscriptionClient.TryCreate(out var transcriptionClient, out var reason))
                {
                    ShowInfo($"Transcription disabled: {reason}");
                    CleanupTempFiles(recording.WavPaths);
                    return;
                }

                string? transcript = null;
                try
                {
                    transcript = RunWithBusyDialog(
                        "Transcribing",
                        "Transcribing audio...",
                        () => TranscribeRecordingAsync(recording, voiceConfig, transcriptionClient!));
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    CleanupTempFiles(recording.WavPaths);
                    return;
                }

                try
                {
                    if (!AiWorkItemClient.TryCreate(out var client, out var failedReason))
                    {
                        ShowInfo($"AI work item generation disabled: {failedReason}");
                        return;
                    }

                    var prompt = BuildWorkItemEditPrompt(item, transcript);
                    var draft = RunWithBusyDialog<WorkItemDraft?>(
                        "Updating",
                        "Generating updated work item...",
                        () => client!.GenerateDraftAsync(prompt));
                    if (draft == null || string.IsNullOrWhiteSpace(draft.Summary))
                    {
                        ShowInfo("AI did not return a valid update draft.");
                        return;
                    }

                    WorkItemService.ApplyEditDraft(item.Path, draft);
                    ReloadItems();
                    ShowInfo($"{item.Id} updated.");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
                finally
                {
                    CleanupTempFiles(recording.WavPaths);
                }
            }

            void RunVoiceDocFromRecording(AudioRecordingResult recording, string docType)
            {
                var voiceConfig = VoiceConfig.Load();
                if (!OpenAiTranscriptionClient.TryCreate(out var transcriptionClient, out var failReason))
                {
                    ShowInfo($"Transcription disabled: {failReason}");
                    CleanupTempFiles(recording.WavPaths);
                    return;
                }

                string? transcript = null;
                try
                {
                    transcript = RunWithBusyDialog(
                        "Transcribing",
                        "Transcribing audio...",
                        () => TranscribeRecordingAsync(recording, voiceConfig, transcriptionClient!));
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    CleanupTempFiles(recording.WavPaths);
                    return;
                }

                try
                {
                    if (!AiDocClient.TryCreate(out var client, out var reason))
                    {
                        ShowInfo($"AI doc generation disabled: {reason}");
                        CleanupTempFiles(recording.WavPaths);
                        return;
                    }

                    DocDraft? draft = RunWithBusyDialog<DocDraft?>(
                        "Generating",
                        "Generating document...",
                        () => client!.GenerateDraftAsync(docType, transcript, titleOverride: null));
                    if (draft == null)
                    {
                        ShowInfo("AI did not return a valid doc draft.");
                        CleanupTempFiles(recording.WavPaths);
                        return;
                    }

                    var title = !string.IsNullOrWhiteSpace(draft.Title)
                        ? draft.Title
                        : DocTitleHelper.FromTranscript(transcript);

                    var body = !string.IsNullOrWhiteSpace(draft.Body)
                        ? draft.Body
                        : DocBodyBuilder.BuildSkeleton(docType, title);

                    var excerpt = DocFrontMatterBuilder.BuildTranscriptExcerpt(transcript, voiceConfig.TranscriptExcerptMaxChars);
                    var source = new DocSourceInfo(
                        "voice",
                        string.IsNullOrWhiteSpace(excerpt) ? null : excerpt,
                        new DocAudioInfo(voiceConfig.Format.SampleRateHz, voiceConfig.Format.Channels, "wav"));

                    var created = DocService.CreateGeneratedDoc(
                        repoRoot,
                        config,
                        docType,
                        title,
                        body,
                        path: null,
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        "draft",
                        source,
                        force: false);

                    docsAll = LoadDocs(repoRoot, config);
                    ApplyDocsFilter();
                    ShowInfo($"Doc created at {created.Path}");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
                finally
                {
                    CleanupTempFiles(recording.WavPaths);
                }
            }

            static string ResolveWorkItemType(string? overrideType, string? generatedType)
            {
                var candidate = overrideType ?? generatedType;
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return "task";
                }
                return candidate.Trim().ToLowerInvariant() switch
                {
                    "bug" => "bug",
                    "task" => "task",
                    "spike" => "spike",
                    _ => "task"
                };
            }

            static T? RunWithBusyDialog<T>(string title, string message, Func<Task<T>> work)
            {
                T? result = default;
                Exception? error = null;

                var dialog = new Dialog(title, 50, 6)
                {
                    ColorScheme = Colors.Dialog
                };
                dialog.Add(new Label(message) { X = 1, Y = 1, Width = Dim.Fill(2) });

                _ = Task.Run(async () =>
                {
                    try
                    {
                        result = await work().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        Application.MainLoop.Invoke(() => Application.RequestStop(dialog));
                    }
                });

                Application.Run(dialog);
                if (error is not null)
                {
                    throw error;
                }

                return result;
            }

            static async Task<string?> TranscribeRecordingAsync(
                AudioRecordingResult recording,
                VoiceConfig config,
                ITranscriptionClient transcriptionClient)
            {
                var transcript = string.Empty;
                var tempFiles = new List<string>();
                try
                {
                    var transcriber = new VoiceTranscriptionService(transcriptionClient, config);
                    var result = await transcriber.TranscribeAsync(recording, CancellationToken.None).ConfigureAwait(false);
                    transcript = result.Transcript.Trim();
                    tempFiles.AddRange(result.TempFiles);
                }
                finally
                {
                    CleanupTempFiles(tempFiles);
                }

                return string.IsNullOrWhiteSpace(transcript) ? null : transcript;
            }

            static void CleanupTempFiles(IEnumerable<string> paths)
            {
                foreach (var path in paths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup failures.
#pragma warning disable ERP022
                    }
#pragma warning restore ERP022
                }
            }

            void ShowCreateDialog()
            {
                var typeField = new TextField("task");
                var titleField = new TextField(string.Empty);
                var statusFieldInput = new TextField("draft");
                var ownerField = new TextField(string.Empty);
                var priorityField = new TextField(string.Empty);

                var previewLabel = new Label("Command: (none)")
                {
                    X = 1,
                    Y = 11,
                    Width = Dim.Fill(2)
                };

                var dialog = new Dialog("New work item", 60, 20)
                {
                    ColorScheme = Colors.Dialog
                };
                var typePickButton = CreatePickerButton(typeField, workItemTypeOptions, "Work item type");
                typeField.ColorScheme = inputScheme;
                titleField.ColorScheme = inputScheme;
                statusFieldInput.ColorScheme = inputScheme;
                ownerField.ColorScheme = inputScheme;
                priorityField.ColorScheme = inputScheme;
                dialog.Add(
                    new Label("Type (task/bug/spike):") { X = 1, Y = 1 },
                    new Label("Title:") { X = 1, Y = 3 },
                    new Label("Status:") { X = 1, Y = 5 },
                    new Label("Owner:") { X = 1, Y = 7 },
                    new Label("Priority:") { X = 1, Y = 9 },
                    typeField, titleField, statusFieldInput, ownerField, priorityField, previewLabel, typePickButton);

                typeField.X = 26;
                typeField.Y = 1;
                typeField.Width = 12;
                titleField.X = 26;
                titleField.Y = 3;
                titleField.Width = Dim.Fill(2);
                statusFieldInput.X = 26;
                statusFieldInput.Y = 5;
                ownerField.X = 26;
                ownerField.Y = 7;
                priorityField.X = 26;
                priorityField.Y = 9;
                typePickButton.X = Pos.Right(typeField) + 1;
                typePickButton.Y = 1;

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var createButton = new Button("Create");
                cancelButton.Clicked += () => Application.RequestStop();
                createButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(createButton);

                void UpdatePreview()
                {
                    var type = typeField.Text?.ToString() ?? "task";
                    var title = titleField.Text?.ToString() ?? string.Empty;
                    var status = statusFieldInput.Text?.ToString();
                    var owner = ownerField.Text?.ToString();
                    var priority = priorityField.Text?.ToString();

                    var command = $"workbench item new --type {type} --title \"{title}\"";
                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        command += $" --status {status}";
                    }
                    if (!string.IsNullOrWhiteSpace(priority))
                    {
                        command += $" --priority {priority}";
                    }
                    if (!string.IsNullOrWhiteSpace(owner))
                    {
                        command += $" --owner {owner}";
                    }
                    if (context.DryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(context, command);
                }

                typeField.TextChanged += _ => UpdatePreview();
                titleField.TextChanged += _ => UpdatePreview();
                statusFieldInput.TextChanged += _ => UpdatePreview();
                ownerField.TextChanged += _ => UpdatePreview();
                priorityField.TextChanged += _ => UpdatePreview();

                UpdatePreview();
                typeField.SetFocus();
                Application.Run(dialog);
                if (!confirmed)
                {
                    return;
                }

                var type = typeField.Text?.ToString() ?? "task";
                var title = titleField.Text?.ToString() ?? string.Empty;
                var status = statusFieldInput.Text?.ToString();
                var owner = ownerField.Text?.ToString();
                var priority = priorityField.Text?.ToString();

                if (string.IsNullOrWhiteSpace(title))
                {
                    ShowInfo("Title is required.");
                    return;
                }

                if (context.DryRunEnabled)
                {
                    ShowInfo("Dry-run enabled; no files were changed.");
                    return;
                }

                try
                {
                    WorkItemService.CreateItem(repoRoot, config, type, title, status, priority, owner);
                    ReloadItems();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void ShowStatusDialog()
            {
                var item = GetSelectedItem();
                if (item is null)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                var statusFieldInput = new TextField(item.Status);
                var noteField = new TextField(string.Empty);

                var previewLabel = new Label("Command: (none)")
                {
                    X = 1,
                    Y = 5,
                    Width = Dim.Fill(2)
                };

                var dialog = new Dialog("Update status", 60, 14)
                {
                    ColorScheme = Colors.Dialog
                };
                statusFieldInput.ColorScheme = inputScheme;
                noteField.ColorScheme = inputScheme;
                dialog.Add(
                    new Label("Status:") { X = 1, Y = 1 },
                    new Label("Note:") { X = 1, Y = 3 },
                    statusFieldInput,
                    noteField,
                    previewLabel);

                statusFieldInput.X = 12;
                statusFieldInput.Y = 1;
                noteField.X = 12;
                noteField.Y = 3;
                noteField.Width = Dim.Fill(2);

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var updateButton = new Button("Update");
                cancelButton.Clicked += () => Application.RequestStop();
                updateButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(updateButton);

                void UpdatePreview()
                {
                    var status = statusFieldInput.Text?.ToString() ?? string.Empty;
                    var note = noteField.Text?.ToString();

                    var command = $"workbench item status {item.Id} {status}";
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        command += $" --note \"{note}\"";
                    }
                    if (context.DryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(context, command);
                }

                statusFieldInput.TextChanged += _ => UpdatePreview();
                noteField.TextChanged += _ => UpdatePreview();
                UpdatePreview();

                statusFieldInput.SetFocus();
                Application.Run(dialog);
                if (!confirmed)
                {
                    return;
                }

                var newStatus = statusFieldInput.Text?.ToString() ?? string.Empty;
                var note = noteField.Text?.ToString();

                if (string.IsNullOrWhiteSpace(newStatus))
                {
                    ShowInfo("Status is required.");
                    return;
                }

                if (context.DryRunEnabled)
                {
                    ShowInfo("Dry-run enabled; no files were changed.");
                    return;
                }

                try
                {
                    WorkItemService.UpdateStatus(item.Path, newStatus, note);
                    ReloadItems();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void ShowDocCreateDialog()
            {
                var item = GetSelectedItem();
                if (item is null)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                var typeField = new TextField("spec");
                var titleField = new TextField(string.Empty);
                var suggestedPath = GetDocsPathSuggestion(config);
                var pathField = new TextField(suggestedPath ?? string.Empty);
                var previewLabel = new Label("Command: (none)")
                {
                    X = 1,
                    Y = 7,
                    Width = Dim.Fill(2)
                };

                var dialog = new Dialog("New doc", 70, 16)
                {
                    ColorScheme = Colors.Dialog
                };
                var typePickButton = CreatePickerButton(typeField, docTypeOptions, "Doc type");
                typeField.ColorScheme = inputScheme;
                titleField.ColorScheme = inputScheme;
                pathField.ColorScheme = inputScheme;
                dialog.Add(
                    new Label("Type (spec/adr/doc/runbook/guide):") { X = 1, Y = 1 },
                    new Label("Title:") { X = 1, Y = 3 },
                    new Label("Path (optional):") { X = 1, Y = 5 },
                    typeField, titleField, pathField, previewLabel, typePickButton);

                typeField.X = 34;
                typeField.Y = 1;
                typeField.Width = 12;
                titleField.X = 34;
                titleField.Y = 3;
                titleField.Width = Dim.Fill(2);
                pathField.X = 34;
                pathField.Y = 5;
                pathField.Width = Dim.Fill(2);
                typePickButton.X = Pos.Right(typeField) + 1;
                typePickButton.Y = 1;

                void UpdatePreview()
                {
                    var type = typeField.Text?.ToString() ?? "spec";
                    var title = titleField.Text?.ToString() ?? string.Empty;
                    var path = pathField.Text?.ToString();

                    var command = $"workbench doc new --type {type} --title \"{title}\" --work-item {item.Id}";
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        command += $" --path \"{path}\"";
                    }
                    if (context.DryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(context, command);
                }

                typeField.TextChanged += _ => UpdatePreview();
                titleField.TextChanged += _ => UpdatePreview();
                pathField.TextChanged += _ => UpdatePreview();
                UpdatePreview();

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var createButton = new Button("Create");
                cancelButton.Clicked += () => Application.RequestStop();
                createButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(createButton);

                typeField.SetFocus();
                Application.Run(dialog);
                if (!confirmed)
                {
                    return;
                }

                var docType = typeField.Text?.ToString() ?? "spec";
                var title = titleField.Text?.ToString() ?? string.Empty;
                var path = pathField.Text?.ToString();

                if (string.IsNullOrWhiteSpace(title))
                {
                    ShowInfo("Title is required.");
                    return;
                }

                if (context.DryRunEnabled)
                {
                    ShowInfo("Dry-run enabled; no files were changed.");
                    return;
                }

                try
                {
                    DocService.CreateDoc(repoRoot, config, docType, title, path, new List<string> { item.Id }, new List<string>(), force: false);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void ShowDocsTabCreateDialog()
            {
                var typeField = new TextField("doc");
                var titleField = new TextField(string.Empty);
                var pathField = new TextField(GetDocsPathSuggestion(config) ?? string.Empty);
                var previewLabel = new Label("Command: (none)")
                {
                    X = 1,
                    Y = 7,
                    Width = Dim.Fill(2)
                };

                var dialog = new Dialog("New doc (Docs tab)", 70, 16)
                {
                    ColorScheme = Colors.Dialog
                };
                var typePickButton = CreatePickerButton(typeField, docTypeOptions, "Doc type");
                typeField.ColorScheme = inputScheme;
                titleField.ColorScheme = inputScheme;
                pathField.ColorScheme = inputScheme;

                dialog.Add(
                    new Label("Type (spec/adr/doc/runbook/guide):") { X = 1, Y = 1 },
                    new Label("Title:") { X = 1, Y = 3 },
                    new Label("Path (optional):") { X = 1, Y = 5 },
                    typeField, titleField, pathField, previewLabel, typePickButton);

                typeField.X = 34;
                typeField.Y = 1;
                typeField.Width = 12;
                titleField.X = 34;
                titleField.Y = 3;
                titleField.Width = Dim.Fill(2);
                pathField.X = 34;
                pathField.Y = 5;
                pathField.Width = Dim.Fill(2);
                typePickButton.X = Pos.Right(typeField) + 1;
                typePickButton.Y = 1;

                void UpdatePreview()
                {
                    var type = typeField.Text?.ToString() ?? "doc";
                    var title = titleField.Text?.ToString() ?? string.Empty;
                    var path = pathField.Text?.ToString();

                    var command = $"workbench doc new --type {type} --title \"{title}\"";
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        command += $" --path \"{path}\"";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(context, command);
                }

                typeField.TextChanged += _ => UpdatePreview();
                titleField.TextChanged += _ => UpdatePreview();
                pathField.TextChanged += _ => UpdatePreview();
                UpdatePreview();

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var createButton = new Button("Create");
                cancelButton.Clicked += () => Application.RequestStop();
                createButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(createButton);

                typeField.SetFocus();
                Application.Run(dialog);
                if (!confirmed)
                {
                    return;
                }

                var docType = typeField.Text?.ToString() ?? "doc";
                var title = titleField.Text?.ToString() ?? string.Empty;
                var path = pathField.Text?.ToString();

                if (string.IsNullOrWhiteSpace(title))
                {
                    ShowInfo("Title is required.");
                    return;
                }

                if (context.DryRunEnabled)
                {
                    ShowInfo("Dry-run enabled; no files were changed.");
                    return;
                }

                try
                {
                    DocService.CreateDoc(repoRoot, config, docType, title, path, new List<string>(), new List<string>(), force: false);
                    docsAll = LoadDocs(repoRoot, config);
                    ApplyDocsFilter();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            listView.SelectedItemChanged += args =>
            {
                if (args.Item < 0 || args.Item >= listItemLookup.Count)
                {
                    UpdateDetails(-1);
                    return;
                }

                if (listItemLookup[args.Item] is null)
                {
                    var nextIndex = FindNextItemIndex(args.Item);
                    if (nextIndex >= 0)
                    {
                        listView.SelectedItem = nextIndex;
                        return;
                    }
                }

                UpdateDetails(args.Item);
            };
            filterField.TextChanged += _ => ApplyFilters();
            statusField.TextChanged += _ => ApplyFilters();

            navFrame.Add(filterLabel, filterField, statusLabel, statusField, statusPickButton, listView);
            detailsFrame.Add(detailsHeader);
            detailsFrame.Add(startWorkButton);
            detailsFrame.Add(completeWorkButton);
            detailsFrame.Add(codexWorkButton);
            detailsFrame.Add(codexHintLabel);
            detailsFrame.Add(detailsBody);
            detailsFrame.Add(detailsDivider);
            detailsFrame.Add(linkTypeLabel);
            detailsFrame.Add(linkTypeField);
            detailsFrame.Add(linkHint);
            detailsFrame.Add(linksList);
            footer.Add(dryRunLabel);
            footer.Add(commandPreviewLabel);
            footer.Add(gitInfoLabel);

            var workTab = new TabView.Tab("Work Items", new View());
            workTab.View.Add(navFrame, detailsFrame);

            var docsTab = new TabView.Tab("Docs", new View());
            docsTab.View.Add(docsFilterLabel, docsFilterField, docsTree, docsPreviewHeader, docsPreview);

            var settingsTab = new TabView.Tab("Settings", new View());
            settingsTab.View.Add(settingsScroll);

            var repoTab = new TabView.Tab("Repo", new View());
            var repoFrame = new FrameView("Repository")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            var repoSummary = new Label("Basic git actions (no conflict handling).")
            {
                X = 1,
                Y = 0
            };
            var pullButton = new Button("Pull")
            {
                X = 1,
                Y = 2
            };
            var stageButton = new Button("Stage all")
            {
                X = Pos.Right(pullButton) + 2,
                Y = 2
            };
            var pushButton = new Button("Push")
            {
                X = Pos.Right(stageButton) + 2,
                Y = 2
            };

            void ExecutePull()
            {
                try
                {
                    SetCommandPreview(context, "git pull --ff-only");
                    var result = GitService.Run(repoRoot, "pull", "--ff-only");
                    if (result.ExitCode != 0)
                    {
                        throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git pull failed.");
                    }
                    UpdateGitInfo();
                    ShowInfo("Pull completed.");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void ExecuteStage()
            {
                try
                {
                    SetCommandPreview(context, "git add -A");
                    var result = GitService.Run(repoRoot, "add", "-A");
                    if (result.ExitCode != 0)
                    {
                        throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git add failed.");
                    }
                    UpdateGitInfo();
                    ShowInfo("Staged all changes.");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void ExecutePush()
            {
                try
                {
                    var branch = GitService.GetCurrentBranch(repoRoot);
                    SetCommandPreview(context, $"git push -u origin {branch}");
                    GitService.Push(repoRoot, branch);
                    UpdateGitInfo();
                    ShowInfo("Push completed.");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            pullButton.Clicked += ExecutePull;
            stageButton.Clicked += ExecuteStage;
            pushButton.Clicked += ExecutePush;
            repoFrame.Add(repoSummary, pullButton, stageButton, pushButton);
            repoTab.View.Add(repoFrame);

            tabView.AddTab(workTab, true);
            tabView.AddTab(docsTab, false);
            tabView.AddTab(settingsTab, false);
            tabView.AddTab(repoTab, false);

            window.Add(tabView, footer);
            top.Add(window);
            statusBar = new StatusBar();
            top.Add(statusBar);

            defaultScheme = top.ColorScheme ?? Colors.Base;
            context.StatusBar = statusBar;
            context.DefaultScheme = defaultScheme;

            void ApplyTheme(string? themeName)
            {
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
                if (statusBar is not null)
                {
                    statusBar.ColorScheme = scheme;
                }
            }

            void UpdateStatusBar()
            {
                var items = new List<StatusItem>
                {
                    new StatusItem(Key.Esc, "~Esc~ Quit", () => Application.RequestStop()),
                    new StatusItem(Key.F1, "~F1~ Work", () => tabView.SelectedTab = workTab),
                    new StatusItem(Key.F2, "~F2~ Docs Tab", () => tabView.SelectedTab = docsTab),
                    new StatusItem(Key.F3, "~F3~ Settings", () => tabView.SelectedTab = settingsTab),
                    new StatusItem(Key.F4, "~F4~ Repo", () => tabView.SelectedTab = repoTab)
                };

                if (tabView.SelectedTab == workTab)
                {
                    items.Add(new StatusItem(Key.F7, "~F7~ Status", ShowStatusDialog));
                    items.Add(new StatusItem(Key.F5, "~F5~ New", ShowCreateDialog));
                    items.Add(new StatusItem(Key.F6, "~F6~ Dry-run", ToggleDryRun));
                    items.Add(new StatusItem(Key.F8, "~F8~ New Doc (Link)", ShowDocCreateDialog));
                    items.Add(new StatusItem(Key.F9, "~F9~ Sync Nav", ShowSyncDialog));
                    items.Add(new StatusItem(Key.F10, "~F10~ Validate Repo", ShowValidateDialog));
                    items.Add(new StatusItem(Key.F11, "~F11~ Voice Edit", ShowVoiceEditDialog));
                    items.Add(new StatusItem(Key.F12, "~F12~ Voice Item", ShowVoiceWorkItemDialog));
                }
                else if (tabView.SelectedTab == docsTab)
                {
                    items.Add(new StatusItem(Key.F5, "~F5~ Open", ActivateSelectedDoc));
                    items.Add(new StatusItem(Key.F8, "~F8~ New Doc Here", ShowDocsTabCreateDialog));
                    items.Add(new StatusItem(Key.F9, "~F9~ Voice Doc", ShowVoiceDocDialog));
                    items.Add(new StatusItem(Key.Enter, "~Enter~ Open", ActivateSelectedDoc));
                }
                else if (tabView.SelectedTab == settingsTab)
                {
                    items.Add(new StatusItem(Key.F5, "~F5~ Save Config", SaveConfigFromFields));
                    items.Add(new StatusItem(Key.F6, "~F6~ Save Creds", SaveCredentialsFromFields));
                    items.Add(new StatusItem(Key.F7, "~F7~ Reload", () =>
                    {
                        context.SettingsLoaded = false;
                        LoadSettingsFields();
                    }));
                }
                else if (tabView.SelectedTab == repoTab)
                {
                    items.Add(new StatusItem(Key.F5, "~F5~ Pull", ExecutePull));
                    items.Add(new StatusItem(Key.F6, "~F6~ Stage All", ExecuteStage));
                    items.Add(new StatusItem(Key.F7, "~F7~ Push", ExecutePush));
                }

                statusBar.Items = items.ToArray();
                statusBar.SetNeedsDisplay();
            }

            linkTypeField.TextChanged += _ =>
            {
                var item = GetSelectedItem();
                if (item is null)
                {
                    return;
                }
                PopulateLinks(item, linkTypeField!, linkHint!, linksList!, linkTargets);
            };

            docsFilterField.TextChanged += _ => ApplyDocsFilter();
            tabView.SelectedTabChanged += (_, _) =>
            {
                UpdateStatusBar();
                UpdateGitInfo();
                if (tabView.SelectedTab == settingsTab && !context.SettingsLoaded)
                {
                    LoadSettingsFields();
                }
            };
            docsTree.SelectionChanged += (_, args) =>
            {
                if (args.NewValue is not TreeNode node)
                {
                    return;
                }

                if (node.Tag is not string path)
                {
                    SelectedDocPath = null;
                    docsPreview.Text = string.Empty;
                    docsPreviewHeader.Text = "Preview";
                    return;
                }

                SelectedDocPath = path;
                if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    docsPreview.Text = string.Empty;
                    docsPreviewHeader.Text = $"Preview: {path}";
                    return;
                }

                var resolved = ResolveDocsLink(repoRoot, config, path);
                docsPreviewHeader.Text = $"Preview: {path}";
                try
                {
                    var content = File.ReadAllText(resolved);
                    docsPreview.Text = content;
                }
                catch (Exception ex)
                {
                    docsPreview.Text = ex.ToString();
                }
            };

            docsTree.ObjectActivated += args =>
            {
                if (args.ActivatedObject is not TreeNode node || node.Tag is not string path)
                {
                    return;
                }

                if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var resolved = ResolveDocsLink(repoRoot, config, path);
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
            };

            linksList.OpenSelectedItem += args =>
            {
                if (args.Item < 0 || args.Item >= linkTargets.Count)
                {
                    return;
                }

                var target = linkTargets[args.Item];
                if (TryShowPreviewForLink(target))
                {
                    return;
                }
                var resolved = ResolveLink(repoRoot, target);
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
            };

            ApplyFilters();
            ApplyDocsFilter();
            ApplyTheme(config.Tui.Theme);
            UpdateStatusBar();
            UpdateGitInfo();
            Application.Run();
        }
        finally
        {
            Application.Shutdown();
        }

        return 0;
    }

}
