using LocalWiki.App.Models;
using LocalWiki.App.Publishing;
using LocalWiki.App.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalWiki.Tests;

public sealed class CoreTests
{
    [Theory]
    [InlineData("Spacecraft Designer", "spacecraft-designer")]
    [InlineData("  Many   Spaces  ", "many-spaces")]
    [InlineData("A : B / C", "a-b-c")]
    [InlineData("CON", "con-page")]
    public void Slug_generation_is_safe(string title, string expected) => Assert.Equal(expected, MarkdownFileService.Slugify(title, []));

    [Fact]
    public void Slug_collision_and_empty_title()
    {
        Assert.Equal("page-3", MarkdownFileService.Slugify("Page", ["page", "page-2"]));
        Assert.Throws<ArgumentException>(() => MarkdownFileService.Slugify(" - : ", []));
    }

    [Fact]
    public void File_paths_cannot_escape_workspace()
    {
        using var test = new TestWiki();
        Assert.EndsWith("index.md", test.Files.PathFor("index"));
        Assert.Throws<ArgumentException>(() => test.Files.PathFor("../secret"));
        Assert.Throws<ArgumentException>(() => WorkspaceService.SafeChild(test.Workspace.Pages, "C:\\other.md", ".md"));
        Assert.Throws<ArgumentException>(() => WorkspaceService.SafeChild(test.Workspace.Pages, "bad.txt", ".md"));
        Assert.Throws<ArgumentException>(() => test.Assets.PathFor("../secret.png"));
    }

    [Fact]
    public void Navigation_moves_preserve_files_and_descendants()
    {
        using var test = new TestWiki();
        var a = test.Nav.Add("A", "a"); var b = test.Nav.Add("B", "b");
        var child = test.Nav.Add("Child", "child", a.Id);
        var grandchild = test.Nav.Add("Grandchild", "grandchild", child.Id);
        test.Files.Create("a", "# A"); test.Files.Create("b", "# B"); test.Files.Create("child", "# Child"); test.Files.Create("grandchild", "# Grandchild");
        test.Nav.Move(child.Id, b.Id, 0);
        Assert.Equal(b.Id, test.Nav.Parent(child.Id)?.Id);
        Assert.Equal(grandchild.Id, test.Nav.Find(child.Id)?.Children.Single().Id);
        Assert.True(test.Files.Exists("child"));
        test.Nav.Move(child.Id, null, 0);
        Assert.Null(test.Nav.Parent(child.Id));
        test.Nav.Move(b.Id, null, 0);
        Assert.Equal(b.Id, test.Nav.Document.Nodes[0].Id);
        Assert.Throws<ArgumentException>(() => test.Nav.Move(child.Id, grandchild.Id, 0));
        Assert.Throws<ArgumentException>(() => test.Nav.Move(child.Id, child.Id, 0));
        test.Nav.Load();
        Assert.Equal(grandchild.Id, test.Nav.Find(child.Id)?.Children.Single().Id);
        Assert.Equal(child.Id, test.Nav.Document.Nodes[1].Id);
    }

    [Fact]
    public void Navigation_detects_duplicate_ids()
    {
        var id = Guid.NewGuid();
        Assert.Throws<InvalidDataException>(() => NavigationService.Validate(new WikiNavigationDocument { Nodes = [new() { Id = id, Slug = "one" }, new() { Id = id, Slug = "two" }] }));
    }

    [Fact]
    public void Wiki_links_resolve_titles_aliases_slugs_and_backlinks()
    {
        using var test = new TestWiki();
        var target = test.Nav.Add("Rocket Design", "rocket-design");
        test.Nav.Rename(target.Id, "Spacecraft Designer");
        var source = test.Nav.Add("Source", "source");
        test.Files.Create("rocket-design", "# Rocket");
        test.Files.Create("source", "See [[Rocket Design|old name]] and `[[Ignored]]`.\n```\n[[Also ignored]]\n```");
        var parsed = test.Links.Parse(test.Files.Read("source")!.Markdown);
        Assert.Single(parsed);
        Assert.Equal("old name", parsed[0].Label);
        Assert.Equal(target.Id, test.Links.Resolve("Rocket Design")?.Id);
        Assert.Equal(target.Id, test.Links.Resolve("rocket-design")?.Id);
        Assert.Null(test.Links.Resolve("absent"));
        Assert.Equal(source.Id, test.Links.Backlinks("rocket-design", test.Files).Single().Id);
        Assert.Contains("rocket-design.html", test.Links.ToMarkdownLinks("[[Spacecraft Designer]]", true));
    }

    [Fact]
    public void Visual_editor_escaping_is_normalized_before_save()
    {
        Assert.Equal("See [[Home]].", WikiLinkService.NormalizeVisualMarkdown("See \\[\\[Home]]."));
    }

    [Fact]
    public void Files_are_atomic_and_conflicts_are_detected()
    {
        using var test = new TestWiki();
        Assert.Null(test.Files.Read("unknown"));
        test.Files.Create("draft", "first");
        var initial = test.Files.Read("draft")!;
        test.Files.Save("draft", "second", initial.Hash);
        Assert.Throws<IOException>(() => test.Files.Save("draft", "stale", initial.Hash));
        Assert.Equal("second", test.Files.Read("draft")?.Markdown);
        Assert.Empty(Directory.GetFiles(test.Workspace.Pages, "*.tmp"));
        test.Files.Delete("draft");
        Assert.False(test.Files.Exists("draft"));
    }

    [Fact]
    public void Image_upload_validates_type_and_size()
    {
        using var test = new TestWiki();
        Assert.Throws<ArgumentException>(() => test.Assets.Save("bad.png", [1, 2, 3]));
        Assert.StartsWith("../Assets/shot-", test.Assets.Save("shot.png", [137, 80, 78, 71, 13, 10, 26, 10]));
    }

    [Fact]
    public void Markdown_rendering_blocks_raw_html_and_unsafe_links()
    {
        using var test = new TestWiki();
        var html = test.Renderer.Render("<script>alert(1)</script>\n\n[click](javascript:alert(1))", false).Html;
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Published_site_is_portable_and_has_search_navigation_links_and_assets()
    {
        using var test = new TestWiki();
        var page = test.Nav.Add("Spacecraft", "spacecraft");
        test.Files.Create("spacecraft", "# Spacecraft\n\nBack to [[Home]].\n\n![Ship](../Assets/ship.png)");
        File.WriteAllBytes(test.Assets.PathFor("ship.png"), [137, 80, 78, 71]);
        var publisher = new StaticPublisherService(test.Workspace, test.Nav, test.Files, test.Renderer, test.Links, new TestEnvironment(test.WebRoot), NullLogger<StaticPublisherService>.Instance);
        await publisher.PublishAsync();
        Assert.True(File.Exists(Path.Combine(test.Workspace.Published, "index.html")));
        var html = File.ReadAllText(Path.Combine(test.Workspace.Published, "spacecraft.html"));
        Assert.Contains("Home", html);
        Assert.Contains("index.html", html);
        Assert.Contains("Assets/ship.png", html);
        Assert.Contains("static/wiki.css", html);
        Assert.DoesNotContain("localhost", html, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(test.Workspace.Published, "Assets", "ship.png")));
        Assert.Contains("Spacecraft", File.ReadAllText(Path.Combine(test.Workspace.Published, "static", "search-index.js")));
        File.WriteAllText(Path.Combine(test.Workspace.Published, "stale.html"), "old publication");
        var saved = test.Files.Read("spacecraft")!;
        test.Files.Save("spacecraft", "# Updated spacecraft", saved.Hash);
        await publisher.PublishAsync();
        Assert.False(File.Exists(Path.Combine(test.Workspace.Published, "stale.html")));
        Assert.Contains("Updated spacecraft", File.ReadAllText(Path.Combine(test.Workspace.Published, "spacecraft.html")));
        Assert.Empty(Directory.EnumerateDirectories(test.Workspace.Root, "Published.stage-*"));
        Assert.Empty(Directory.EnumerateDirectories(test.Workspace.Root, "Published.previous-*"));
    }

    private sealed class TestWiki : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "localwiki-test-" + Guid.NewGuid().ToString("N"));
        public string WebRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/LocalWiki.App/wwwroot"));
        public WorkspaceService Workspace { get; } = new();
        public NavigationService Nav { get; }
        public MarkdownFileService Files { get; }
        public AssetService Assets { get; }
        public WikiLinkService Links { get; }
        public MarkdownRenderService Renderer { get; }
        public TestWiki()
        {
            Workspace.Open(Root);
            Nav = new(Workspace); Nav.Load();
            Files = new(Workspace); Assets = new(Workspace);
            Links = new(Nav); Renderer = new(Links);
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private sealed class TestEnvironment(string webRoot) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "LocalWiki.Tests";
        public string WebRootPath { get; set; } = webRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = webRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
