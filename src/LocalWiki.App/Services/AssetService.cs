using System.Text.RegularExpressions;

namespace LocalWiki.App.Services;

public sealed class AssetService(WorkspaceService workspace)
{
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    { [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp", [".gif"] = "image/gif" };
    public string PathFor(string name)
    {
        var extension = Path.GetExtension(name);
        if (!Types.ContainsKey(extension)) throw new ArgumentException("Unsupported image type.");
        return WorkspaceService.SafeChild(workspace.Assets, name, extension);
    }
    public string Save(string name, byte[] data)
    {
        if (data.Length == 0 || data.Length > 10 * 1024 * 1024) throw new ArgumentException("Image must be between 1 byte and 10 MB.");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!Types.ContainsKey(extension)) throw new ArgumentException("Unsupported image type.");
        var valid = extension switch
        {
            ".png" => data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff,
            ".gif" => data.Length >= 6 && System.Text.Encoding.ASCII.GetString(data, 0, 6) is "GIF87a" or "GIF89a",
            ".webp" => data.Length >= 12 && System.Text.Encoding.ASCII.GetString(data, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(data, 8, 4) == "WEBP",
            _ => false
        };
        if (!valid) throw new ArgumentException("Image contents do not match the file type.");
        var stem = Regex.Replace(Path.GetFileNameWithoutExtension(name).ToLowerInvariant(), "[^a-z0-9-]+", "-").Trim('-');
        if (stem.Length == 0) stem = "image";
        var generated = stem + "-" + Guid.NewGuid().ToString("N")[..8] + extension;
        File.WriteAllBytes(PathFor(generated), data);
        return "../Assets/" + generated;
    }
    public (string Path, string Type)? Resolve(string name)
    {
        try { var path = PathFor(name); return File.Exists(path) ? (path, Types[Path.GetExtension(path)]) : null; }
        catch (ArgumentException) { return null; }
    }
}
