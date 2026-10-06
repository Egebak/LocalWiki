using System.Net;
using System.Text;
using System.Text.Json;
using LocalWiki.App.Models;
using LocalWiki.App.Services;

namespace LocalWiki.App.Publishing;

public sealed class StaticPublisherService(WorkspaceService ws, NavigationService nav, MarkdownFileService files, MarkdownRenderService renderer, WikiLinkService links, IWebHostEnvironment environment, ILogger<StaticPublisherService> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task PublishAsync()
    {
        if (!ws.IsOpen) return;
        await gate.WaitAsync();
        var stage = Path.Combine(ws.Root, "Published.stage-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(ws.Root, "Published.previous-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(stage);
            Directory.CreateDirectory(Path.Combine(stage, "static"));
            CopyDirectory(ws.Assets, Path.Combine(stage, "Assets"));
            File.Copy(Path.Combine(environment.WebRootPath, "app.css"), Path.Combine(stage, "static", "wiki.css"));
            File.Copy(Path.Combine(environment.WebRootPath, "js", "staticWiki.js"), Path.Combine(stage, "static", "wiki.js"));
            var entries = nav.All().ToList();
            var search = new List<object>();
            foreach (var node in entries)
            {
                var markdown = files.Read(node.Slug)?.Markdown ?? "# Missing page\n\nThe Markdown file for this page is missing.";
                var rendered = renderer.Render(markdown, true);
                var crumbs = nav.Breadcrumb(node.Id);
                var html = PageHtml(node, rendered, crumbs, entries);
                await File.WriteAllTextAsync(Path.Combine(stage, node.Slug + ".html"), html, new UTF8Encoding(false));
                search.Add(new { slug = node.Slug, title = node.Title, aliases = node.Aliases, breadcrumb = string.Join(" › ", crumbs.Select(c => c.Title)), text = markdown.Length > 2000 ? markdown[..2000] : markdown });
            }
            if (!File.Exists(Path.Combine(stage, "index.html")))
            {
                if (entries.Count == 0) await File.WriteAllTextAsync(Path.Combine(stage, "index.html"), "<!doctype html><title>LocalWiki</title><p>No pages yet.</p>");
                else File.Copy(Path.Combine(stage, entries[0].Slug + ".html"), Path.Combine(stage, "index.html"));
            }
            await File.WriteAllTextAsync(Path.Combine(stage, "static", "search-index.js"), "window.localWikiSearchIndex=" + JsonSerializer.Serialize(search, JsonOptions.Options) + ";", new UTF8Encoding(false));
            if (Directory.Exists(ws.Published)) await MoveWithRetryAsync(ws.Published, backup);
            try { await MoveWithRetryAsync(stage, ws.Published); }
            catch
            {
                if (Directory.Exists(backup)) await MoveWithRetryAsync(backup, ws.Published);
                throw;
            }
            if (Directory.Exists(backup))
            {
                try { await DeleteWithRetryAsync(backup); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The new publication is already in place. A locked backup can be removed later.
                    logger.LogWarning(ex, "Could not remove previous publication at {Path}", backup);
                }
            }
            logger.LogInformation("Published {Count} pages at {Path}", entries.Count, ws.Published);
        }
        catch (Exception ex) { logger.LogError(ex, "Static publication failed"); throw; }
        finally
        {
            try
            {
                if (Directory.Exists(stage)) await DeleteWithRetryAsync(stage);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not remove publication staging directory at {Path}", stage);
            }
            finally { gate.Release(); }
        }
    }
    private static async Task MoveWithRetryAsync(string source, string destination)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Move(source, destination); return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 5)
            {
                await Task.Delay(100 * (attempt + 1));
            }
        }
    }
    private static async Task DeleteWithRetryAsync(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Delete(path, true); return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 5)
            {
                await Task.Delay(100 * (attempt + 1));
            }
        }
    }
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(source)) CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
    private string PageHtml(WikiPageNode page, RenderedPage body, IReadOnlyList<WikiPageNode> crumbs, IReadOnlyList<WikiPageNode> all)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>").Append(E(page.Title)).Append(" · LocalWiki</title><link rel=\"stylesheet\" href=\"static/wiki.css\"></head><body><div class=\"app-shell\"><aside class=\"sidebar\"><div class=\"brand\">LocalWiki</div><div class=\"workspace-name\">").Append(E(ws.Name)).Append("</div><input id=\"wiki-search\" class=\"search-input\" placeholder=\"Search pages…\" aria-label=\"Search pages\"><div id=\"search-results\"></div><nav class=\"tree\">");
        AppendTree(sb, nav.Document.Nodes, page.Id);
        sb.Append("</nav></aside><main class=\"page-main\"><div class=\"breadcrumbs\">");
        foreach (var c in crumbs) sb.Append("<a href=\"").Append(E(c.Slug)).Append(".html\">").Append(E(c.Title)).Append("</a><span>›</span>");
        sb.Append("</div><header class=\"page-header\"><h1>").Append(E(page.Title)).Append("</h1></header><article class=\"markdown-body\">").Append(body.Html).Append("</article>");
        var backlinks = all.Where(n => n.Id != page.Id && files.Read(n.Slug) is { } p && links.Parse(p.Markdown).Any(l => links.Resolve(l.Target)?.Id == page.Id)).ToList();
        if (backlinks.Count > 0) { sb.Append("<section class=\"backlinks\"><h2>Referenced by</h2>"); foreach (var b in backlinks) sb.Append("<a href=\"").Append(E(b.Slug)).Append(".html\">").Append(E(b.Title)).Append("</a>"); sb.Append("</section>"); }
        sb.Append("</main><aside class=\"toc\"><div>On this page</div>");
        foreach (var heading in body.Headings) sb.Append("<a href=\"#").Append(E(heading.Id)).Append("\" style=\"padding-left:").Append((heading.Level - 1) * 12).Append("px\">").Append(E(heading.Text)).Append("</a>");
        sb.Append("</aside></div><script src=\"static/search-index.js\"></script><script src=\"static/wiki.js\"></script></body></html>");
        return sb.ToString();
    }
    private static void AppendTree(StringBuilder sb, IEnumerable<WikiPageNode> nodes, Guid current)
    {
        sb.Append("<ul>");
        foreach (var n in nodes)
        {
            sb.Append("<li><a class=\"").Append(n.Id == current ? "active" : "").Append("\" href=\"").Append(WebUtility.HtmlEncode(n.Slug)).Append(".html\">").Append(WebUtility.HtmlEncode(n.Title)).Append("</a>");
            if (n.Children.Count > 0) AppendTree(sb, n.Children, current);
            sb.Append("</li>");
        }
        sb.Append("</ul>");
    }
}
