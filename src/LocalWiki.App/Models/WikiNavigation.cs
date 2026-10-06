namespace LocalWiki.App.Models;

public sealed class WikiNavigationDocument
{
    public int Version { get; set; } = 1;
    public List<WikiPageNode> Nodes { get; set; } = [];
}

public sealed class WikiPageNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
    public List<WikiPageNode> Children { get; set; } = [];
}

public sealed record PageSnapshot(string Markdown, string Hash, DateTime ModifiedUtc);
public sealed record SearchHit(string Slug, string Title, string Breadcrumb, string Excerpt);
public sealed record HeadingLink(string Id, string Text, int Level);
public sealed record RenderedPage(string Html, IReadOnlyList<HeadingLink> Headings);
