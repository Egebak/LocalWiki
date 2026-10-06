using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LocalWiki.App.Models;

namespace LocalWiki.App.Services;

public sealed class MarkdownFileService(WorkspaceService workspace)
{
    public string PathFor(string slug)
    {
        if (!Regex.IsMatch(slug, "^[a-z0-9][a-z0-9-]*$")) throw new ArgumentException("Invalid page slug.");
        return WorkspaceService.SafeChild(workspace.Pages, slug + ".md", ".md");
    }
    public IEnumerable<string> Slugs() => Directory.EnumerateFiles(workspace.Pages, "*.md", SearchOption.TopDirectoryOnly).Select(Path.GetFileNameWithoutExtension)!;
    public bool Exists(string slug) => File.Exists(PathFor(slug));
    public PageSnapshot? Read(string slug)
    {
        var path = PathFor(slug);
        if (!File.Exists(path)) return null;
        var bytes = File.ReadAllBytes(path);
        return new(Encoding.UTF8.GetString(bytes), Convert.ToHexString(SHA256.HashData(bytes)), File.GetLastWriteTimeUtc(path));
    }
    public PageSnapshot Save(string slug, string markdown, string? expectedHash = null, bool force = false)
    {
        var path = PathFor(slug);
        var current = Read(slug);
        if (!force && current is not null && expectedHash != current.Hash) throw new IOException("This page changed on disk. Review the conflict before overwriting it.");
        AtomicFile.Write(path, markdown);
        return Read(slug)!;
    }
    public void Create(string slug, string markdown)
    {
        var path = PathFor(slug);
        if (File.Exists(path)) throw new IOException("Page already exists.");
        AtomicFile.Write(path, markdown);
    }
    public void Delete(string slug) { var path = PathFor(slug); if (File.Exists(path)) File.Delete(path); }

    public static string Slugify(string title, IEnumerable<string> used)
    {
        var normalized = title.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[^a-z0-9]+", "-").Trim('-');
        if (normalized.Length == 0) throw new ArgumentException("Title needs letters or numbers.");
        if (Regex.IsMatch(normalized, "^(con|prn|aux|nul|com[1-9]|lpt[1-9])$", RegexOptions.IgnoreCase)) normalized += "-page";
        var set = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);
        var slug = normalized;
        for (var n = 2; set.Contains(slug); n++) slug = normalized + "-" + n;
        return slug;
    }
}
