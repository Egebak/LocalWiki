using System.Text.Json;
using System.Text.RegularExpressions;
using LocalWiki.App.Models;

namespace LocalWiki.App.Services;

public sealed class NavigationService(WorkspaceService workspace)
{
    private readonly object gate = new();
    public WikiNavigationDocument Document { get; private set; } = new();
    public void Load()
    {
        lock (gate)
        {
            var loaded = JsonSerializer.Deserialize<WikiNavigationDocument>(File.ReadAllText(workspace.NavigationPath), JsonOptions.Options) ?? throw new InvalidDataException("Empty navigation file.");
            Validate(loaded);
            Document = loaded;
        }
    }
    public void Save()
    {
        lock (gate)
        {
            Validate(Document);
            AtomicFile.Write(workspace.NavigationPath, JsonSerializer.Serialize(Document, JsonOptions.Options));
        }
    }
    public static IEnumerable<WikiPageNode> Flatten(IEnumerable<WikiPageNode> nodes)
    {
        foreach (var node in nodes) { yield return node; foreach (var child in Flatten(node.Children)) yield return child; }
    }
    public IEnumerable<WikiPageNode> All() => Flatten(Document.Nodes);
    public WikiPageNode? Find(Guid id) => All().FirstOrDefault(n => n.Id == id);
    public WikiPageNode? FindSlug(string slug) => All().FirstOrDefault(n => string.Equals(n.Slug, slug, StringComparison.OrdinalIgnoreCase));
    public WikiPageNode? Parent(Guid id) => All().FirstOrDefault(n => n.Children.Any(c => c.Id == id));
    public IReadOnlyList<WikiPageNode> Breadcrumb(Guid id)
    {
        var result = new List<WikiPageNode>();
        var node = Find(id);
        while (node is not null) { result.Insert(0, node); node = Parent(node.Id); }
        return result;
    }
    public WikiPageNode Add(string title, string slug, Guid? parentId = null)
    {
        lock (gate)
        {
            if (FindSlug(slug) is not null) throw new ArgumentException("Slug already exists.");
            var list = parentId is null ? Document.Nodes : Find(parentId.Value)?.Children ?? throw new ArgumentException("Parent not found.");
            var node = new WikiPageNode { Title = title.Trim(), Slug = slug };
            list.Add(node);
            Save();
            return node;
        }
    }
    public void Rename(Guid id, string title)
    {
        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Enter a title.");
            var node = Find(id) ?? throw new ArgumentException("Page not found.");
            if (!string.Equals(node.Title, title.Trim(), StringComparison.Ordinal) && !node.Aliases.Contains(node.Title, StringComparer.OrdinalIgnoreCase)) node.Aliases.Add(node.Title);
            node.Title = title.Trim(); Save();
        }
    }
    public IReadOnlyList<WikiPageNode> Remove(Guid id, bool subtree)
    {
        lock (gate)
        {
            var parent = Parent(id);
            var list = parent?.Children ?? Document.Nodes;
            var index = list.FindIndex(n => n.Id == id);
            if (index < 0) throw new ArgumentException("Page not found.");
            var node = list[index];
            var deleted = subtree ? Flatten([node]).ToList() : new List<WikiPageNode> { node };
            list.RemoveAt(index);
            if (!subtree) list.InsertRange(index, node.Children);
            Save();
            return deleted;
        }
    }
    public void Move(Guid id, Guid? newParentId, int newIndex)
    {
        lock (gate)
        {
            var node = Find(id) ?? throw new ArgumentException("Page not found.");
            if (newParentId == id || Flatten(node.Children).Any(n => n.Id == newParentId)) throw new ArgumentException("A page cannot be moved into itself or a descendant.");
            var destination = newParentId is null ? Document.Nodes : Find(newParentId.Value)?.Children ?? throw new ArgumentException("Destination not found.");
            var source = Parent(id)?.Children ?? Document.Nodes;
            if (!source.Remove(node)) throw new InvalidDataException("Page is absent from its parent.");
            destination.Insert(Math.Clamp(newIndex, 0, destination.Count), node);
            Save();
        }
    }
    public static void Validate(WikiNavigationDocument doc)
    {
        if (doc.Version != 1) throw new InvalidDataException("Unsupported navigation version.");
        var ids = new HashSet<Guid>(); var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ancestry = new HashSet<WikiPageNode>(ReferenceEqualityComparer.Instance);
        void Visit(IEnumerable<WikiPageNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.Id == Guid.Empty || !ids.Add(n.Id)) throw new InvalidDataException("Duplicate or empty page ID.");
                if (!Regex.IsMatch(n.Slug, "^[a-z0-9][a-z0-9-]*$") || !slugs.Add(n.Slug)) throw new InvalidDataException("Invalid or duplicate slug.");
                if (!ancestry.Add(n)) throw new InvalidDataException("Circular hierarchy.");
                Visit(n.Children);
                ancestry.Remove(n);
            }
        }
        Visit(doc.Nodes);
    }
}
