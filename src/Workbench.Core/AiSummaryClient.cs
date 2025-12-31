using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Workbench;

public sealed class AiSummaryClient
{
    private const int DefaultMaxSummaryChars = 240;
    private readonly AIAgent agent;
    private readonly int maxSummaryChars;

    private AiSummaryClient(AIAgent agent, int maxSummaryChars)
    {
        this.agent = agent;
        this.maxSummaryChars = maxSummaryChars;
    }

    public static bool TryCreate(out AiSummaryClient? client, out string? reason)
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

        var model = Environment.GetEnvironmentVariable("WORKBENCH_AI_MODEL")
            ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? "gpt-4o-mini";
        var maxChars = DefaultMaxSummaryChars;
        if (int.TryParse(Environment.GetEnvironmentVariable("WORKBENCH_AI_SUMMARY_MAX_CHARS"), CultureInfo.InvariantCulture, out var parsed))
        {
            maxChars = Math.Clamp(parsed, 80, 600);
        }

        var instructions = Environment.GetEnvironmentVariable("WORKBENCH_AI_SUMMARY_INSTRUCTIONS")
            ?? "You summarize markdown doc changes for change logs. Respond in 1-2 sentences, plain text only.";

        AIAgent aiAgent = new OpenAIClient(apiKey)
            .GetChatClient(model)
            .CreateAIAgent(instructions: instructions, name: "WorkbenchSummarizer");

        client = new AiSummaryClient(aiAgent, maxChars);
        return true;
    }

    public async Task<string?> SummarizeAsync(string diffText)
    {
        var prompt = $"Summarize the following git diff for a markdown document. " +
                     $"Return 1-2 sentences, no bullets, no markdown.\n\n{diffText}";
        AgentRunResponse? summary = await agent.RunAsync(prompt).ConfigureAwait(false);
        if (summary == null)
        {
            return null;
        }

        var normalized = string.Join(' ', summary.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (normalized.Length > maxSummaryChars)
        {
            normalized = normalized[..maxSummaryChars].TrimEnd() + "…";
        }
        return normalized;
    }
}
