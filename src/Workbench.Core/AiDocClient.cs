using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;

namespace Workbench.Core;

public sealed class AiDocClient
{
    private const string DefaultInstructions = """
        You create Workbench documentation drafts from user input.
        Output JSON only (no markdown, no code fences) with these fields:
        - title: concise title, 3-10 words.
        - body: markdown content without YAML front matter.
        Use clear headings and avoid placeholders.
        When the body references repository-local files, folders, classes, interfaces, or symbols, render them as clickable relative Markdown links and keep inline code styling inside the link text, for example [`ValidationService`](../src/Workbench.Core/ValidationService.cs).
        Use absolute URLs only for external resources such as NuGet package pages or other web-hosted documentation.
        """;

    private const string DefaultPrompt = """
        Create a Workbench {0} document draft using the template below.
        Follow the section order and adapt headings as needed.
        {1}

        Input:
        {2}
        """;

    private readonly AIAgent agent;

    private AiDocClient(AIAgent agent)
    {
        this.agent = agent;
    }

    public static bool TryCreate(out AiDocClient? client, out string? reason)
    {
        client = null;
        reason = null;

        var provider = Environment.GetEnvironmentVariable("WORKBENCH_AI_PROVIDER") ?? "openai";
        if (string.Equals(provider, "none", StringComparison.OrdinalIgnoreCase))
        {
            reason = "WORKBENCH_AI_PROVIDER=none";
            return false;
        }
        if (!string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Unsupported provider '{provider}'.";
            return false;
        }

        var apiKey = Environment.GetEnvironmentVariable("WORKBENCH_AI_OPENAI_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            reason = "Missing OPENAI_API_KEY or WORKBENCH_AI_OPENAI_KEY.";
            return false;
        }

        var model = Environment.GetEnvironmentVariable("WORKBENCH_AI_DOC_MODEL")
            ?? Environment.GetEnvironmentVariable("WORKBENCH_AI_MODEL")
            ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? "gpt-4o-mini";

        var instructions = Environment.GetEnvironmentVariable("WORKBENCH_AI_DOC_INSTRUCTIONS")
            ?? DefaultInstructions;

        AIAgent aiAgent = new OpenAIClient(apiKey)
            .GetChatClient(model)
            .CreateAIAgent(instructions: instructions, name: "WorkbenchDocGenerator");

        client = new AiDocClient(aiAgent);
        return true;
    }

    public async Task<DocDraft?> GenerateDraftAsync(string docType, string input, string? titleOverride)
    {
        if (string.IsNullOrWhiteSpace(docType) || string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var template = DocPromptTemplates.BuildTemplate(docType.Trim());
        var titleHint = string.IsNullOrWhiteSpace(titleOverride)
            ? string.Empty
            : $"\nUse this title exactly: {titleOverride.Trim()}";
        var prompt = string.Format(CultureInfo.InvariantCulture, DefaultPrompt, docType.Trim(), template + titleHint, input.Trim());

        var response = await this.agent.RunAsync(prompt).ConfigureAwait(false);
        if (response == null || string.IsNullOrWhiteSpace(response.Text))
        {
            return null;
        }

        if (!TryExtractJson(response.Text, out var json))
        {
            return null;
        }

        DocDraft? draft = null;
        try
        {
            draft = JsonSerializer.Deserialize(json, Workbench.Core.WorkbenchJsonContext.Default.DocDraft);
        }
        catch (JsonException)
        {
            return null;
        }

        if (draft == null)
        {
            return null;
        }

        var normalizedTitle = draft.Title?.Trim() ?? string.Empty;
        var normalizedBody = draft.Body?.Trim() ?? string.Empty;

        return new DocDraft(normalizedTitle, normalizedBody);
    }

    private static bool TryExtractJson(string text, out string json)
    {
        json = text.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var lines = json.Split('\n').ToList();
            if (lines.Count > 0)
            {
                lines.RemoveAt(0);
            }
            if (lines.Count > 0 && lines[^1].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                lines.RemoveAt(lines.Count - 1);
            }
            json = string.Join("\n", lines).Trim();
        }

        if (json.StartsWith("{", StringComparison.Ordinal))
        {
            return true;
        }

        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            json = json[start..(end + 1)];
            return true;
        }

        return false;
    }
}
