using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LocalWiki.App.Models;

namespace LocalWiki.App.Services;

public sealed class WorkspaceService
{
    public string Root { get; private set; } = "";
    public bool IsOpen => Root.Length > 0;
    public string Name => IsOpen ? Path.GetFileName(Root.TrimEnd(Path.DirectorySeparatorChar)) : "LocalWiki";
    public string Pages => Path.Combine(Root, "Pages");
    public string Assets => Path.Combine(Root, "Assets");
    public string Wiki => Path.Combine(Root, ".wiki");
    public string Published => Path.Combine(Root, "Published");
    public string NavigationPath => Path.Combine(Wiki, "navigation.json");

    public void Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Enter a workspace directory.");
        Root = Path.GetFullPath(path.Trim().Trim('"'));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Pages);
        Directory.CreateDirectory(Assets);
        Directory.CreateDirectory(Wiki);
        Directory.CreateDirectory(Published);
        if (!File.Exists(NavigationPath))
        {
            var madeHome = !Directory.EnumerateFiles(Pages, "*.md").Any();
            if (madeHome)
                File.WriteAllText(Path.Combine(Pages, "index.md"), "# Home\n\nWelcome to your documentation workspace.\n", new UTF8Encoding(false));
            var nodes = Directory.EnumerateFiles(Pages, "*.md")
                .Select(file => new WikiPageNode { Slug = Path.GetFileNameWithoutExtension(file), Title = madeHome && Path.GetFileNameWithoutExtension(file) == "index" ? "Home" : TitleFromSlug(Path.GetFileNameWithoutExtension(file)) }).ToList();
            AtomicFile.Write(NavigationPath, JsonSerializer.Serialize(new WikiNavigationDocument { Nodes = nodes }, JsonOptions.Options));
        }
    }

    public void Close() => Root = "";

    public static string TitleFromSlug(string slug) => string.Join(' ', slug.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(s => char.ToUpperInvariant(s[0]) + s[1..]));

    public static string SafeChild(string root, string relative, string extension)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Contains('/') || relative.Contains(':') || relative.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || relative is "." or "..")
            throw new ArgumentException("Invalid file name.");
        if (!string.Equals(Path.GetExtension(relative), extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid file type.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("File escapes workspace.");
        return path;
    }
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}

public static class AtomicFile
{
    public static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = new UTF8Encoding(false).GetBytes(content);
                stream.Write(bytes);
                stream.Flush(true);
            }
            for (var attempt = 0; ; attempt++)
            {
                try { File.Move(temp, path, true); break; }
                catch (IOException) when (attempt < 5) { Thread.Sleep(50 * (attempt + 1)); }
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
