using Workbench.Core;
using Workbench.Core.Voice;
using Workbench.VoiceViz;

namespace Workbench.Tui;

using System.Diagnostics;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.Trees;
using Workbench;
using Workbench.Tui.VoiceViz;

public static class TuiEntrypoint
{
    public static async Task<int> RunAsync(string[] args)
    {
        var repoRoot = Repository.FindRepoRoot(Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            await Console.Error.WriteLineAsync("Not a git repository.").ConfigureAwait(false);
            return 2;
        }

        var config = WorkbenchConfig.Load(repoRoot, out _);
        var allItems = LoadItems(repoRoot, config);
        var filteredItems = new List<WorkItem>(allItems);
        var workItemStatusOptions = new[] { "all", "draft", "ready", "in-progress", "blocked", "done", "dropped" };
        var workItemTypeOptions = new[] { "task", "bug", "spike" };
        var docTypeOptions = new[] { "spec", "adr", "doc", "runbook", "guide" };

        Application.Init();
        try
        {
            var top = Application.Top;
            var inputScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Black, Color.White),
                Focus = Application.Driver.MakeAttribute(Color.Black, Color.White)
            };
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
                Height = Dim.Fill(2)
            };

            var navFrame = new FrameView("Work Items")
            {
                X = 0,
                Y = 0,
                Width = 44,
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
                Height = 2
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
                Width = Dim.Fill(7)
            };

            var statusPickButton = CreatePickerButton(statusField, workItemStatusOptions, "Status filter");
            statusPickButton.X = Pos.Right(statusField) + 1;
            statusPickButton.Y = 1;

            var listView = new ListView(filteredItems.Select(item => $"{item.Id} {item.Title}").ToList())
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

            var linkTargets = new List<string>();
            var linksList = new ListView(new List<string>())
            {
                X = 0,
                Y = Pos.Bottom(detailsHeader) + 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var linkTypeLabel = new Label("Links:")
            {
                X = 1,
                Y = Pos.Bottom(detailsHeader)
            };

            var linkTypeField = new TextField("all")
            {
                X = Pos.Right(linkTypeLabel) + 1,
                Y = Pos.Bottom(detailsHeader),
                Width = 12
            };

            var linkHint = new Label(string.Empty)
            {
                X = Pos.Right(linkTypeField) + 2,
                Y = Pos.Bottom(detailsHeader),
                Width = Dim.Fill()
            };

            var dryRunEnabled = false;
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

            void SetCommandPreview(string command)
            {
                var prefix = dryRunEnabled ? "DRY-RUN: " : string.Empty;
                commandPreviewLabel.Text = $"{prefix}Command: {command}";
            }

            void ShowError(Exception ex)
            {
                MessageBox.ErrorQuery("Error", ex.Message, "Ok");
            }

            void ShowInfo(string message)
            {
                MessageBox.Query("Info", message, "Ok");
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
                    SetCommandPreview($"open \"{resolvedPath}\"");
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
                    SetCommandPreview($"preview \"{resolved}\"");
                    ShowDocPreviewDialog(trimmedLink, resolved, content);
                    return true;
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    return true;
                }
            }

            int FindOptionIndex(IReadOnlyList<string> options, string? current)
            {
                if (string.IsNullOrWhiteSpace(current))
                {
                    return -1;
                }

                for (var i = 0; i < options.Count; i++)
                {
                    if (string.Equals(options[i], current, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                return -1;
            }

            string? ShowPickDialog(string title, IReadOnlyList<string> options, string? current)
            {
                var optionList = options.ToList();
                var dialog = new Dialog(title, 40, 14);
                var list = new ListView(optionList)
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill(2),
                    Height = Dim.Fill(3)
                };
                var selectedIndex = FindOptionIndex(optionList, current);
                if (selectedIndex >= 0)
                {
                    list.SelectedItem = selectedIndex;
                }
                dialog.Add(list);

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var selectButton = new Button("Select");
                cancelButton.Clicked += () => Application.RequestStop();
                selectButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(selectButton);

                Application.Run(dialog);
                if (!confirmed || list.SelectedItem < 0 || list.SelectedItem >= optionList.Count)
                {
                    return null;
                }

                return optionList[list.SelectedItem];
            }

            Button CreatePickerButton(TextField field, IReadOnlyList<string> options, string title)
            {
                var button = new Button("Pick");
                button.Clicked += () =>
                {
                    var selection = ShowPickDialog(title, options, field.Text?.ToString());
                    if (!string.IsNullOrWhiteSpace(selection))
                    {
                        field.Text = selection;
                    }
                };
                return button;
            }

            void UpdateDetails(int index)
            {
                if (index < 0 || index >= filteredItems.Count)
                {
                    detailsHeader.Text = "Select a work item to see details.";
                    linkTargets.Clear();
                    linksList.SetSource(new List<string>());
                    linkHint.Text = string.Empty;
                    SetCommandPreview("(none)");
                    return;
                }

                var item = filteredItems[index];
                var specsCount = item.Related.Specs.Count;
                var adrsCount = item.Related.Adrs.Count;
                var filesCount = item.Related.Files.Count;
                var issuesCount = item.Related.Issues.Count;
                var prsCount = item.Related.Prs.Count;

                detailsHeader.Text = $"[{item.Status}] {item.Id}\n{item.Title}\n\nOwner: {item.Owner ?? "?"}\nPriority: {item.Priority ?? "?"}\nUpdated: {item.Updated ?? "?"}\n\nLinked: specs {specsCount}, adrs {adrsCount}, files {filesCount}\nIssues: {issuesCount}, PRs: {prsCount}\n\nEnter: open selected link";
                SetCommandPreview($"workbench item show {item.Id}");

                PopulateLinks(item, linkTypeField, linkHint, linksList, linkTargets);
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
                    .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                listView.SetSource(filteredItems.Select(item => $"{item.Id} {item.Title}").ToList());
                UpdateDetails(listView.SelectedItem);
            }

            void ReloadItems()
            {
                allItems = LoadItems(repoRoot, config);
                ApplyFilters();
            }

            var docsAll = LoadDocs(repoRoot, config);
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

            void OpenSelectedItem()
            {
                if (listView.SelectedItem < 0 || listView.SelectedItem >= filteredItems.Count)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                var item = filteredItems[listView.SelectedItem];
                SetCommandPreview($"open \"{item.Path}\"");
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = item.Path,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }

            void ActivateSelectedDoc()
            {
                docsTree.ActivateSelectedObjectIfAny();
            }

            void OpenLinkedDocs()
            {
                if (listView.SelectedItem < 0 || listView.SelectedItem >= filteredItems.Count)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                var item = filteredItems[listView.SelectedItem];
                var links = item.Related.Specs
                    .Concat(item.Related.Adrs)
                    .Concat(item.Related.Files)
                    .Where(link => !string.IsNullOrWhiteSpace(link))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (links.Count == 0)
                {
                    ShowInfo("No linked docs or files.");
                    return;
                }

                var dialog = new Dialog("Open linked doc", 70, 18);
                var list = new ListView(links)
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill(2),
                    Height = Dim.Fill(2)
                };
                dialog.Add(list);

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var openButton = new Button("Open");
                cancelButton.Clicked += () => Application.RequestStop();
                openButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(openButton);

                Application.Run(dialog);
                if (!confirmed || list.SelectedItem < 0 || list.SelectedItem >= links.Count)
                {
                    return;
                }

                var link = links[list.SelectedItem];
                var resolved = ResolveLink(repoRoot, link);
                SetCommandPreview($"open \"{resolved}\"");
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

            void OpenLinkedIssues()
            {
                if (listView.SelectedItem < 0 || listView.SelectedItem >= filteredItems.Count)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                var item = filteredItems[listView.SelectedItem];
                var links = item.Related.Issues
                    .Concat(item.Related.Prs)
                    .Where(link => !string.IsNullOrWhiteSpace(link))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (links.Count == 0)
                {
                    ShowInfo("No linked issues or PRs.");
                    return;
                }

                var dialog = new Dialog("Open issue/PR", 70, 18);
                var list = new ListView(links)
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill(2),
                    Height = Dim.Fill(2)
                };
                dialog.Add(list);

                var confirmed = false;
                var cancelButton = new Button("Cancel");
                var openButton = new Button("Open");
                cancelButton.Clicked += () => Application.RequestStop();
                openButton.Clicked += () =>
                {
                    confirmed = true;
                    Application.RequestStop();
                };
                dialog.AddButton(cancelButton);
                dialog.AddButton(openButton);

                Application.Run(dialog);
                if (!confirmed || list.SelectedItem < 0 || list.SelectedItem >= links.Count)
                {
                    return;
                }

                var link = links[list.SelectedItem];
                SetCommandPreview($"open \"{link}\"");
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = link,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
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
                    if (dryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(command);
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
                if (dryRunEnabled)
                {
                    command += " --dry-run";
                }
                SetCommandPreview(command);

                try
                {
                    var result = await NavigationService.SyncNavigationAsync(
                            repoRoot,
                            config,
                            includeDone,
                            syncIssues,
                            force,
                            workboard,
                            dryRunEnabled)
                        .ConfigureAwait(false);
                    var summary = $"Docs: {result.DocsUpdated}, Items: {result.ItemsUpdated}, Index: {result.IndexFilesUpdated}, Workboard: {result.WorkboardUpdated}";
                    if (dryRunEnabled)
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
                    SetCommandPreview(command);
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
                dryRunEnabled = !dryRunEnabled;
                dryRunLabel.Text = dryRunEnabled ? "Dry-run: ON" : "Dry-run: OFF";
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
                    if (dryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(command);
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

                if (dryRunEnabled)
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
                if (listView.SelectedItem < 0 || listView.SelectedItem >= filteredItems.Count)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                var item = filteredItems[listView.SelectedItem];
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
                    if (dryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(command);
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

                if (dryRunEnabled)
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
                if (listView.SelectedItem < 0 || listView.SelectedItem >= filteredItems.Count)
                {
                    ShowInfo("Select a work item first.");
                    return;
                }

                var item = filteredItems[listView.SelectedItem];
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
                    if (dryRunEnabled)
                    {
                        command += " --dry-run";
                    }
                    previewLabel.Text = $"Command: {command}";
                    SetCommandPreview(command);
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

                if (dryRunEnabled)
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
                    SetCommandPreview(command);
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

                if (dryRunEnabled)
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

            listView.SelectedItemChanged += args => UpdateDetails(args.Item);
            filterField.TextChanged += _ => ApplyFilters();
            statusField.TextChanged += _ => ApplyFilters();

            navFrame.Add(filterLabel, filterField, statusLabel, statusField, statusPickButton, listView);
            detailsFrame.Add(detailsHeader);
            detailsFrame.Add(linkTypeLabel);
            detailsFrame.Add(linkTypeField);
            detailsFrame.Add(linkHint);
            detailsFrame.Add(linksList);
            footer.Add(dryRunLabel);
            footer.Add(commandPreviewLabel);

            var workTab = new TabView.Tab("Work Items", new View());
            workTab.View.Add(navFrame, detailsFrame);

            var docsTab = new TabView.Tab("Docs", new View());
            docsTab.View.Add(docsFilterLabel, docsFilterField, docsTree, docsPreviewHeader, docsPreview);

            tabView.AddTab(workTab, true);
            tabView.AddTab(docsTab, false);

            window.Add(tabView, footer);
            top.Add(window);
            var statusBar = new StatusBar();
            top.Add(statusBar);

            void UpdateStatusBar()
            {
                var items = new List<StatusItem>
                {
                    new StatusItem(Key.Esc, "~Esc~ Quit", () => Application.RequestStop()),
                    new StatusItem(Key.F1, "~F1~ Work", () => tabView.SelectedTab = workTab),
                    new StatusItem(Key.F2, "~F2~ Docs Tab", () => tabView.SelectedTab = docsTab)
                };

                if (tabView.SelectedTab == workTab)
                {
                    items.Add(new StatusItem(Key.F3, "~F3~ New", ShowCreateDialog));
                    items.Add(new StatusItem(Key.F4, "~F4~ Status", ShowStatusDialog));
                    items.Add(new StatusItem(Key.F5, "~F5~ Open", OpenSelectedItem));
                    items.Add(new StatusItem(Key.F6, "~F6~ Links", OpenLinkedDocs));
                    items.Add(new StatusItem(Key.F7, "~F7~ Issues", OpenLinkedIssues));
                    items.Add(new StatusItem(Key.F8, "~F8~ New Doc (Link)", ShowDocCreateDialog));
                    items.Add(new StatusItem(Key.F9, "~F9~ Sync Nav", ShowSyncDialog));
                    items.Add(new StatusItem(Key.F10, "~F10~ Validate Repo", ShowValidateDialog));
                    items.Add(new StatusItem(Key.F11, "~F11~ Dry-run", ToggleDryRun));
                    items.Add(new StatusItem(Key.F12, "~F12~ Voice Item", ShowVoiceWorkItemDialog));
                    items.Add(new StatusItem(Key.CtrlMask | Key.R, "~^R~ Voice Item", ShowVoiceWorkItemDialog));
                }
                else
                {
                    items.Add(new StatusItem(Key.F5, "~F5~ Open", ActivateSelectedDoc));
                    items.Add(new StatusItem(Key.F8, "~F8~ New Doc Here", ShowDocsTabCreateDialog));
                    items.Add(new StatusItem(Key.F9, "~F9~ Voice Doc", ShowVoiceDocDialog));
                    items.Add(new StatusItem(Key.Enter, "~Enter~ Open", ActivateSelectedDoc));
                }

                statusBar.Items = items.ToArray();
                statusBar.SetNeedsDisplay();
            }

            linkTypeField.TextChanged += _ =>
            {
                if (listView.SelectedItem < 0 || listView.SelectedItem >= filteredItems.Count)
                {
                    return;
                }
                PopulateLinks(filteredItems[listView.SelectedItem], linkTypeField, linkHint, linksList, linkTargets);
            };

            docsFilterField.TextChanged += _ => ApplyDocsFilter();
            tabView.SelectedTabChanged += (_, _) => UpdateStatusBar();
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
                SetCommandPreview($"open \"{resolved}\"");
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
                SetCommandPreview($"open \"{resolved}\"");
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
            UpdateStatusBar();
            Application.Run();
        }
        finally
        {
            Application.Shutdown();
        }

        return 0;
    }

    private static List<WorkItem> LoadItems(string repoRoot, WorkbenchConfig config)
    {
        return WorkItemService.ListItems(repoRoot, config, includeDone: false).Items
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> LoadDocs(string repoRoot, WorkbenchConfig config)
    {
        var docsRoot = Path.Combine(repoRoot, config.Paths.DocsRoot);
        if (!Directory.Exists(docsRoot))
        {
            return new List<string>();
        }

        var rootPrefix = config.Paths.DocsRoot.TrimEnd('/', '\\') + "/";
        return Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .Select(path => path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? path[rootPrefix.Length..]
                : path)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ITreeNode> BuildDocsTree(IList<string> docs)
    {
        var roots = new List<ITreeNode>();
        foreach (var doc in docs)
        {
            var parts = doc.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentList = (IList<ITreeNode>)roots;
            var currentPath = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                var existing = currentList
                    .OfType<TreeNode>()
                    .FirstOrDefault(node => string.Equals(node.Text, part, StringComparison.Ordinal));
                if (existing is null)
                {
                    existing = new TreeNode(part)
                    {
                        Children = new List<ITreeNode>()
                    };
                    currentList.Add(existing);
                }

                existing.Tag = currentPath;
                if (existing.Children is null)
                {
                    existing.Children = new List<ITreeNode>();
                }
                currentList = existing.Children;
            }
        }

        return roots;
    }

    private static bool ShouldIncludeLink(string linkType, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        var normalized = filter.Trim().ToLowerInvariant();
        if (normalized is "all" or "*")
        {
            return true;
        }

        return string.Equals(linkType, normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static void PopulateLinks(
        WorkItem item,
        TextField linkTypeField,
        Label linkHint,
        ListView linksList,
        List<string> linkTargets)
    {
        var filter = linkTypeField.Text?.ToString() ?? string.Empty;
        linkTargets.Clear();
        var linkLabels = new List<string>();

        foreach (var link in item.Related.Specs)
        {
            if (!ShouldIncludeLink("spec", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"spec: {link}");
        }
        foreach (var link in item.Related.Adrs)
        {
            if (!ShouldIncludeLink("adr", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"adr: {link}");
        }
        foreach (var link in item.Related.Files)
        {
            if (!ShouldIncludeLink("file", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"file: {link}");
        }
        foreach (var link in item.Related.Issues)
        {
            if (!ShouldIncludeLink("issue", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"issue: {link}");
        }
        foreach (var link in item.Related.Prs)
        {
            if (!ShouldIncludeLink("pr", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"pr: {link}");
        }

        linksList.SetSource(linkLabels);
        var counts = $"spec {item.Related.Specs.Count}, adr {item.Related.Adrs.Count}, file {item.Related.Files.Count}, issue {item.Related.Issues.Count}, pr {item.Related.Prs.Count}";
        if (linkLabels.Count > 0)
        {
            linksList.SelectedItem = 0;
            linkHint.Text = $"{counts} | Enter: open selected link";
        }
        else
        {
            linkHint.Text = $"{counts} | No links for this filter.";
        }
    }

    private static List<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0)
            .ToList();
    }

    private static string ResolveLink(string repoRoot, string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        var trimmed = link.TrimStart('/');
        var combined = Path.Combine(repoRoot, trimmed);
        return Path.GetFullPath(combined);
    }

    private static string ResolveDocsLink(string repoRoot, WorkbenchConfig config, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        var trimmed = path.TrimStart('/');
        var docsRoot = config.Paths.DocsRoot.TrimEnd('/', '\\');
        var combined = Path.Combine(repoRoot, docsRoot, trimmed);
        return Path.GetFullPath(combined);
    }

    private static string? GetDocsPathSuggestion(WorkbenchConfig config)
    {
        var selected = SelectedDocPath;
        if (string.IsNullOrWhiteSpace(selected))
        {
            return null;
        }

        if (selected.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(selected)?.Replace('\\', '/');
            return string.IsNullOrWhiteSpace(dir) ? null : PrefixDocsRoot(config, dir);
        }

        return PrefixDocsRoot(config, selected.Replace('\\', '/'));
    }

    private static string? SelectedDocPath { get; set; }

    private static string PrefixDocsRoot(WorkbenchConfig config, string path)
    {
        var docsRoot = config.Paths.DocsRoot.TrimEnd('/', '\\');
        return $"{docsRoot}/{path.TrimStart('/', '\\')}";
    }

}
