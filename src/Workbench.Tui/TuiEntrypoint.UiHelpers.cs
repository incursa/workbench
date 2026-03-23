using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terminal.Gui;

namespace Workbench.Tui;

public static partial class TuiEntrypoint
{
    private static void SetCommandPreview(TuiContext context, string command)
    {
        var label = context.CommandPreviewLabel
            ?? throw new InvalidOperationException("Command preview label not initialized.");
        var prefix = context.DryRunEnabled ? "DRY-RUN: " : string.Empty;
        label.Text = $"{prefix}Command: {command}";
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.ErrorQuery("Error", ex.Message, "Ok");
    }

    private static void ShowInfo(string message)
    {
        MessageBox.Query("Info", message, "Ok");
    }

    private static bool EnsureSettingsFile(string path, string displayName, string defaultContent)
    {
        if (File.Exists(path))
        {
            return true;
        }

        var choice = MessageBox.Query(
            "Create file",
            $"{displayName} not found. Create it in the current repo?",
            "Create",
            "Cancel");
        if (choice != 0)
        {
            return false;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, defaultContent);
        return true;
    }

    private static bool TryParseEnvLine(string rawLine, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            return false;
        }

        if (line.StartsWith("export ", StringComparison.Ordinal))
        {
            line = line[7..].TrimStart();
        }

        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        key = line[..separator].Trim();
        if (key.Length == 0)
        {
            return false;
        }

        value = line[(separator + 1)..].Trim();
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                value = value[1..^1];
            }
        }

        return true;
    }

    private static string? GetEnvValue(IEnumerable<string> lines, string key)
    {
        return TryGetEnvValue(lines, key, out var value) ? value : null;
    }

    private static bool TryGetEnvValue(IEnumerable<string> lines, string key, out string? value)
    {
        value = null;
        foreach (var line in lines)
        {
            if (!TryParseEnvLine(line, out var parsedKey, out var parsedValue))
            {
                continue;
            }
            if (string.Equals(parsedKey, key, StringComparison.Ordinal))
            {
                value = parsedValue;
                return true;
            }
        }

        return false;
    }

    private static void SetEnvValue(List<string> lines, string key, string? value)
    {
        var hasValue = !string.IsNullOrWhiteSpace(value);
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (!TryParseEnvLine(lines[i], out var parsedKey, out _))
            {
                continue;
            }
            if (!string.Equals(parsedKey, key, StringComparison.Ordinal))
            {
                continue;
            }

            if (!hasValue)
            {
                lines.RemoveAt(i);
            }
            else
            {
                lines[i] = $"{key}={value}";
            }
            return;
        }

        if (hasValue)
        {
            lines.Add($"{key}={value}");
        }
    }

    private static int GetStatusRank(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "planned" => 0,
            "in_progress" => 1,
            "blocked" => 2,
            "complete" => 3,
            "cancelled" => 4,
            "superseded" => 5,
            _ => 6
        };
    }

    private static int GetPriorityRank(string? priority)
    {
        return priority?.ToLowerInvariant() switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" => 2,
            "low" => 3,
            _ => 4
        };
    }

    private static string FormatStatusLabel(string? status, bool useEmoji)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return useEmoji ? "❔ unknown" : "??";
        }

        if (!useEmoji)
        {
            return status.ToLowerInvariant() switch
            {
                "planned" => "..",
                "in_progress" => ">>",
                "blocked" => "!!",
                "complete" => "##",
                "cancelled" => "--",
                "superseded" => "~>",
                _ => "??"
            };
        }

        return status.ToLowerInvariant() switch
        {
            "planned" => "🟡 planned",
            "in_progress" => "🔵 in-progress",
            "blocked" => "🟥 blocked",
            "complete" => "✅ complete",
            "cancelled" => "⚪ cancelled",
            "superseded" => "↩ superseded",
            _ => status
        };
    }

    private static string FormatPriorityLabel(string? priority, bool useEmoji)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return "-";
        }

        if (!useEmoji)
        {
            return priority.ToLowerInvariant() switch
            {
                "critical" => "!!!!",
                "high" => "!!!",
                "medium" => "!!",
                "low" => "!",
                _ => "?"
            };
        }

        return priority.ToLowerInvariant() switch
        {
            "critical" => "🔴 critical",
            "high" => "🟠 high",
            "medium" => "🟡 medium",
            "low" => "🟢 low",
            _ => priority
        };
    }

    private static int FindOptionIndex(IReadOnlyList<string> options, string? current)
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

    private static string? ShowPickDialog(string title, IReadOnlyList<string> options, string? current)
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

    private static Button CreatePickerButton(TextField field, IReadOnlyList<string> options, string title)
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
}
