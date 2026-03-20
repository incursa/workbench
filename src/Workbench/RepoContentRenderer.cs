using System.Net;
using Markdig;

namespace Workbench;

public static class RepoContentRenderer
{
    private static readonly MarkdownPipeline markdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string RenderMarkdown(string markdown)
    {
        return Markdown.ToHtml(markdown ?? string.Empty, markdownPipeline);
    }

    public static string EncodeText(string text)
    {
        return WebUtility.HtmlEncode(text ?? string.Empty);
    }

    public static bool IsMarkdownPath(string path)
    {
        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);
    }
}
