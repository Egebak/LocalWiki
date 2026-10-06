using System.Text.RegularExpressions;
using LocalWiki.App.Models;

namespace LocalWiki.App.Services;

public sealed class SearchService(NavigationService nav, MarkdownFileService files)
{
    private readonly Dictionary<string, (WikiPageNode Node, string Body)> index = new(StringComparer.OrdinalIgnoreCase);
    public void Rebuild()
    {
        index.Clear();
        foreach (var node in nav.All()) index[node.Slug] = (node, files.Read(node.Slug)?.Markdown ?? "");
    }
    public void Update(string slug) { var n = nav.FindSlug(slug); if (n is not null) index[slug] = (n, files.Read(slug)?.Markdown ?? ""); }
    public void Remove(string slug) => index.Remove(slug);
    public IReadOnlyList<SearchHit> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        return index.Values.Select(entry =>
        {
            var n = entry.Node; var body = Regex.Replace(entry.Body, "[`#*\\[\\]()>]", " ");
            var title = n.Title.Contains(query, StringComparison.OrdinalIgnoreCase);
            var alias = n.Aliases.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase));
            var slug = n.Slug.Contains(query, StringComparison.OrdinalIgnoreCase);
            var pos = body.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            var score = title ? 4 : alias ? 3 : slug ? 2 : pos >= 0 ? 1 : 0;
            var excerpt = pos < 0 ? body[..Math.Min(140, body.Length)] : body[Math.Max(0, pos - 45)..Math.Min(body.Length, pos + 95)];
            return (score, hit: new SearchHit(n.Slug, n.Title, string.Join(" › ", nav.Breadcrumb(n.Id).Select(b => b.Title)), excerpt.Trim()));
        }).Where(x => x.score > 0).OrderByDescending(x => x.score).Take(50).Select(x => x.hit).ToList();
    }
}
