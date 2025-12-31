namespace Workbench.Tui;

using System.Diagnostics;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.Trees;
using Workbench;

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

        Application.Init();
        try
        {
            var top = Application.Top;
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
                Width = 36,
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
                Width = Dim.Fill(1)
            };

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
            docsPreview.ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
                Focus = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
                HotNormal = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
                HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.Gray),
                Disabled = Application.Driver.MakeAttribute(Color.Black, Color.Gray)
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

                var includeDoneCheck = new CheckBox("Include done items") { X = 1, Y = 1, Checked = includeDone };
                var syncIssuesCheck = new CheckBox("Sync issue links") { X = 1, Y = 2, Checked = syncIssues };
                var forceCheck = new CheckBox("Force index rewrite") { X = 1, Y = 3, Checked = force };
                var workboardCheck = new CheckBox("Regenerate workboard") { X = 1, Y = 4, Checked = workboard };
                var previewLabel = new Label("Command: (none)") { X = 1, Y = 6, Width = Dim.Fill(2) };

                var dialog = new Dialog("Sync navigation", 70, 14);
                dialog.Add(includeDoneCheck, syncIssuesCheck, forceCheck, workboardCheck, previewLabel);

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
                var skipDocSchemaCheck = new CheckBox("Skip doc schema") { X = 1, Y = 1, Checked = false };
                var includeLabel = new Label("Link include (comma-separated):") { X = 1, Y = 3 };
                var includeField = new TextField(string.Empty) { X = 1, Y = 4, Width = Dim.Fill(2) };
                var excludeLabel = new Label("Link exclude (comma-separated):") { X = 1, Y = 6 };
                var excludeField = new TextField(string.Empty) { X = 1, Y = 7, Width = Dim.Fill(2) };
                var previewLabel = new Label("Command: (none)") { X = 1, Y = 9, Width = Dim.Fill(2) };

                var dialog = new Dialog("Validate repo", 76, 16);
                dialog.Add(skipDocSchemaCheck, includeLabel, includeField, excludeLabel, excludeField, previewLabel);

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
                typeField.ColorScheme = Colors.Dialog;
                titleField.ColorScheme = Colors.Dialog;
                statusFieldInput.ColorScheme = Colors.Dialog;
                ownerField.ColorScheme = Colors.Dialog;
                priorityField.ColorScheme = Colors.Dialog;
                dialog.Add(
                    new Label("Type (task/bug/spike):") { X = 1, Y = 1 },
                    new Label("Title:") { X = 1, Y = 3 },
                    new Label("Status:") { X = 1, Y = 5 },
                    new Label("Owner:") { X = 1, Y = 7 },
                    new Label("Priority:") { X = 1, Y = 9 },
                    typeField, titleField, statusFieldInput, ownerField, priorityField, previewLabel);

                typeField.X = 26;
                typeField.Y = 1;
                titleField.X = 26;
                titleField.Y = 3;
                titleField.Width = Dim.Fill(2);
                statusFieldInput.X = 26;
                statusFieldInput.Y = 5;
                ownerField.X = 26;
                ownerField.Y = 7;
                priorityField.X = 26;
                priorityField.Y = 9;

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
                statusFieldInput.ColorScheme = Colors.Dialog;
                noteField.ColorScheme = Colors.Dialog;
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
                typeField.ColorScheme = Colors.Dialog;
                titleField.ColorScheme = Colors.Dialog;
                pathField.ColorScheme = Colors.Dialog;
                dialog.Add(
                    new Label("Type (spec/adr/doc/runbook/guide):") { X = 1, Y = 1 },
                    new Label("Title:") { X = 1, Y = 3 },
                    new Label("Path (optional):") { X = 1, Y = 5 },
                    typeField, titleField, pathField, previewLabel);

                typeField.X = 34;
                typeField.Y = 1;
                titleField.X = 34;
                titleField.Y = 3;
                titleField.Width = Dim.Fill(2);
                pathField.X = 34;
                pathField.Y = 5;
                pathField.Width = Dim.Fill(2);

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
                typeField.ColorScheme = Colors.Dialog;
                titleField.ColorScheme = Colors.Dialog;
                pathField.ColorScheme = Colors.Dialog;

                dialog.Add(
                    new Label("Type (spec/adr/doc/runbook/guide):") { X = 1, Y = 1 },
                    new Label("Title:") { X = 1, Y = 3 },
                    new Label("Path (optional):") { X = 1, Y = 5 },
                    typeField, titleField, pathField, previewLabel);

                typeField.X = 34;
                typeField.Y = 1;
                titleField.X = 34;
                titleField.Y = 3;
                titleField.Width = Dim.Fill(2);
                pathField.X = 34;
                pathField.Y = 5;
                pathField.Width = Dim.Fill(2);

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

            navFrame.Add(filterLabel, filterField, statusLabel, statusField, listView);
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
                }
                else
                {
                    items.Add(new StatusItem(Key.F5, "~F5~ Open", ActivateSelectedDoc));
                    items.Add(new StatusItem(Key.F8, "~F8~ New Doc Here", ShowDocsTabCreateDialog));
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
