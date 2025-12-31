using System.Linq;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Workbench;

public sealed class AiWorkItemClient
{
    private const string DefaultInstructions = """
        You create Workbench work item drafts from user input.
        Output JSON only (no markdown, no code fences) with these fields:
        - title: short imperative title (3-8 words).
        - summary: 1-3 short paragraphs describing the work.
        - acceptanceCriteria: array of 3-7 concrete, testable bullets.
        - type: one of bug, task, spike.
        - tags: array of short lowercase tags (optional).
        Use concise, clear language and avoid placeholders.
        """;

    private const string DefaultPrompt = """
        Create a Workbench work item draft from the input below.
        The output must be valid JSON matching the schema in your instructions.

        Input:
        """;

    private readonly AIAgent agent;

    private AiWorkItemClient(AIAgent agent)
    {
        this.agent = agent;
    }

    public static bool TryCreate(out AiWorkItemClient? client, out string? reason)
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

        var model = Environment.GetEnvironmentVariable("WORKBENCH_AI_WORK_ITEM_MODEL")
            ?? Environment.GetEnvironmentVariable("WORKBENCH_AI_MODEL")
            ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? "gpt-4o-mini";

        var instructions = Environment.GetEnvironmentVariable("WORKBENCH_AI_WORK_ITEM_INSTRUCTIONS")
            ?? DefaultInstructions;

        AIAgent aiAgent = new OpenAIClient(apiKey)
            .GetChatClient(model)
            .CreateAIAgent(instructions: instructions, name: "WorkbenchWorkItemGenerator");

        client = new AiWorkItemClient(aiAgent);
        return true;
    }

    public async Task<WorkItemDraft?> GenerateDraftAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var prompt = $"{DefaultPrompt}{input.Trim()}";
        AgentRunResponse? response = await agent.RunAsync(prompt).ConfigureAwait(false);
        if (response == null || string.IsNullOrWhiteSpace(response.Text))
        {
            return null;
        }

        if (!TryExtractJson(response.Text, out var json))
        {
            return null;
        }

        WorkItemDraft? draft = null;
        try
        {
            draft = JsonSerializer.Deserialize(json, WorkbenchJsonContext.Default.WorkItemDraft);
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
        var normalizedSummary = draft.Summary?.Trim();
        var criteria = draft.AcceptanceCriteria?
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim())
            .ToList();
        var tags = draft.Tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .ToList();

        return new WorkItemDraft(
            normalizedTitle,
            normalizedSummary,
            criteria,
            draft.Type?.Trim(),
            tags);
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
