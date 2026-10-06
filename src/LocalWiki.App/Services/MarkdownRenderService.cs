using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Renderers.Html;
using LocalWiki.App.Models;

namespace LocalWiki.App.Services;

public sealed class MarkdownRenderService(WikiLinkService links)
{
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    public RenderedPage Render(string markdown, bool published)
    {
        var source = links.ToMarkdownLinks(markdown, published);
        var document = Markdown.Parse(source, pipeline);
        var headings = new List<HeadingLink>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in document)
        {
            if (block is HeadingBlock heading)
            {
                var text = ExtractText(heading.Inline);
                var id = Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                if (id.Length == 0) id = "section";
                var baseId = id; var n = 2;
                while (!ids.Add(id)) id = baseId + "-" + n++;
                heading.GetAttributes().Id = id;
                headings.Add(new(id, text, heading.Level));
            }
            RewriteLinks(block, published);
        }
        return new(Markdown.ToHtml(document, pipeline), headings);
    }
    private static string ExtractText(ContainerInline? inline) => inline is null ? "" : string.Concat(inline.Select(i => i is LiteralInline literal ? literal.Content.ToString() : i is CodeInline code ? code.Content : ""));
    private static void RewriteLinks(MarkdownObject node, bool published)
    {
        if (node is LinkInline link)
        {
            var url = link.Url ?? "";
            if (url.StartsWith("../Assets/", StringComparison.OrdinalIgnoreCase) && !url[10..].Contains("..") && !url.Contains('\\'))
                link.Url = (published ? "Assets/" : "/workspace-assets/") + Uri.EscapeDataString(url[10..]);
            else if (!(url.StartsWith("/pages/", StringComparison.Ordinal) || url.StartsWith("/new?", StringComparison.Ordinal) || url.StartsWith('#') || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || (!url.Contains(':') && !url.StartsWith("//"))))
                link.Url = "#blocked-link";
        }
        if (node is ContainerBlock cb) foreach (var child in cb) RewriteLinks(child, published);
        if (node is LeafBlock lb && lb.Inline is not null) RewriteLinks(lb.Inline, published);
        if (node is ContainerInline ci) foreach (var child in ci) RewriteLinks(child, published);
    }
}
