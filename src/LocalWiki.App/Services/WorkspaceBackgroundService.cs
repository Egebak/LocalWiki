using System.Text.Json;

namespace LocalWiki.App.Services;

public sealed class WorkspaceBackgroundService(WorkspaceService workspace)
{
    public sealed record BackgroundImage(string Id, string Name, string FileName);

    public sealed class BackgroundDocument
    {
        public string Selected { get; set; } = "none";
        public List<BackgroundImage> Images { get; set; } = [];
    }

    public BackgroundDocument Data { get; private set; } = new();
    public string? BackgroundUrl => Data.Selected == "none" ? null : Data.Selected == "starfield" ? "/images/starfield.jpg" : Data.Images.Any(image => image.Id == Data.Selected) ? BackgroundUrlFor(Data.Selected) : null;
    public static string BackgroundUrlFor(string id) => "/app-background/" + Uri.EscapeDataString(id);

    private string SettingsPath => Path.Combine(workspace.Wiki, "appearance.json");
    private string BackgroundDirectory => Path.Combine(workspace.Wiki, "backgrounds");

    public void Load()
    {
        if (!workspace.IsOpen) { Reset(); return; }
        try { Data = File.Exists(SettingsPath) ? JsonSerializer.Deserialize<BackgroundDocument>(File.ReadAllText(SettingsPath), JsonOptions.Options) ?? new() : new(); }
        catch (Exception) { Data = new(); }
        Data.Selected ??= "none";
        Data.Images ??= [];
    }

    public void Reset() => Data = new();

    public void SelectBackground(string id)
    {
        if (id is not ("none" or "starfield") && !Data.Images.Any(image => image.Id == id)) throw new ArgumentException("Unknown background.", nameof(id));
        var previous = Data.Selected;
        Data.Selected = id;
        try { Save(); }
        catch { Data.Selected = previous; throw; }
    }

    public async Task<string> AddBackgroundAsync(string name, Stream image)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) throw new ArgumentException("Choose a JPG, PNG, WebP, or GIF image.", nameof(name));
        if (!workspace.IsOpen) throw new InvalidOperationException("Open a workspace first.");
        var id = Guid.NewGuid().ToString("N");
        var fileName = id + extension;
        Directory.CreateDirectory(BackgroundDirectory);
        var path = Path.Combine(BackgroundDirectory, fileName);
        var previous = Data.Selected;
        try
        {
            await using (var target = File.Create(path)) await image.CopyToAsync(target);
            Data.Images.Add(new(id, Path.GetFileName(name), fileName));
            Data.Selected = id;
            Save();
            return id;
        }
        catch
        {
            Data.Images.RemoveAll(item => item.Id == id);
            Data.Selected = previous;
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public (string Path, string ContentType)? ResolveBackground(string id)
    {
        if (!workspace.IsOpen || !Guid.TryParseExact(id, "N", out _)) return null;
        var image = Data.Images.FirstOrDefault(item => item.Id == id);
        if (image is null) return null;
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (image.FileName != id + extension) return null;
        var path = Path.Combine(BackgroundDirectory, image.FileName);
        if (!File.Exists(path)) return null;
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => null
        };
        return contentType is null ? null : (path, contentType);
    }

    private void Save()
    {
        if (!workspace.IsOpen) throw new InvalidOperationException("Open a workspace first.");
        AtomicFile.Write(SettingsPath, JsonSerializer.Serialize(Data, JsonOptions.Options));
    }
}
