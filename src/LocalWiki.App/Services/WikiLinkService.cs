using System.Text;
using System.Text.RegularExpressions;
using LocalWiki.App.Models;

namespace LocalWiki.App.Services;

public sealed record WikiLink(string Target, string Label);

public sealed class WikiLinkService(NavigationService navigation)
{
    public static string NormalizeVisualMarkdown(string markdown) => Regex.Replace(markdown, @"\\\[\\\[(?<inner>[^\]\r\n]+)\]\]", m => "[[" + m.Groups["inner"].Value + "]]");
    public WikiPageNode? Resolve(string target)
    {
        var nodes = navigation.All().ToList();
        return nodes.FirstOrDefault(n => n.Title.Equals(target, StringComparison.Ordinal))
            ?? nodes.FirstOrDefault(n => n.Title.Equals(target, StringComparison.OrdinalIgnoreCase))
            ?? nodes.FirstOrDefault(n => n.Aliases.Any(a => a.Equals(target, StringComparison.OrdinalIgnoreCase)))
            ?? nodes.FirstOrDefault(n => n.Slug.Equals(target, StringComparison.OrdinalIgnoreCase));
    }
    public IReadOnlyList<WikiLink> Parse(string markdown)
    {
        var result = new List<WikiLink>();
        Transform(markdown, link => { result.Add(link); return ""; });
        return result;
    }
    public string ToMarkdownLinks(string markdown, bool published) => Transform(markdown, link =>
    {
        var node = Resolve(link.Target);
        var label = link.Label.Replace("\\", "\\\\").Replace("]", "\\]");
        var url = node is null ? (published ? "#missing" : "/new?title=" + Uri.EscapeDataString(link.Target)) : published ? node.Slug + ".html" : "/pages/" + Uri.EscapeDataString(node.Slug);
        return $"[{label}]({url})";
    });
    public IReadOnlyList<WikiPageNode> Backlinks(string targetSlug, MarkdownFileService files)
    {
        return navigation.All().Where(n => n.Slug != targetSlug && files.Read(n.Slug) is { } page && Parse(page.Markdown).Any(link => Resolve(link.Target)?.Slug == targetSlug)).ToList();
    }
    private static string Transform(string markdown, Func<WikiLink, string> replacement)
    {
        var output = new StringBuilder();
        var lines = markdown.Split('\n');
        var fenced = false;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal) || line.TrimStart().StartsWith("~~~", StringComparison.Ordinal)) fenced = !fenced;
            if (fenced || line.TrimStart().StartsWith("    ", StringComparison.Ordinal)) output.Append(line);
            else
            {
                var inCode = false;
                for (var i = 0; i < line.Length; i++)
                {
                    if (line[i] == '`') { inCode = !inCode; output.Append(line[i]); continue; }
                    if (!inCode && i + 1 < line.Length && line[i] == '[' && line[i + 1] == '[')
                    {
                        var end = line.IndexOf("]]", i + 2, StringComparison.Ordinal);
                        if (end > i + 2)
                        {
                            var parts = line[(i + 2)..end].Split('|', 2);
                            var target = parts[0].Trim();
                            if (target.Length > 0)
                            {
                                output.Append(replacement(new WikiLink(target, parts.Length == 2 ? parts[1].Trim() : target)));
                                i = end + 1; continue;
                            }
                        }
                    }
                    output.Append(line[i]);
                }
            }
            if (lineIndex < lines.Length - 1) output.Append('\n');
        }
        return output.ToString();
    }
}
