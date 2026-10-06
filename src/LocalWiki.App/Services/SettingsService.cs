using System.Text.Json;

namespace LocalWiki.App.Services;

public sealed class SettingsService
{
    public sealed class SettingsDocument
    {
        public string? LastWorkspace { get; set; }
        public List<string> RecentWorkspaces { get; set; } = [];
    }
    public string SettingsPath { get; }
    public SettingsDocument Data { get; private set; } = new();
    public SettingsService()
    {
        var baseDir = Environment.GetEnvironmentVariable("LOCALWIKI_SETTINGS_DIR") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        SettingsPath = Path.Combine(baseDir, "LocalWiki", "settings.json");
        try { if (File.Exists(SettingsPath)) Data = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(SettingsPath), JsonOptions.Options) ?? new(); }
        catch (Exception) { Data = new(); }
    }
    public void Remember(string path)
    {
        Data.LastWorkspace = path;
        Data.RecentWorkspaces.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        Data.RecentWorkspaces.Insert(0, path);
        Data.RecentWorkspaces = Data.RecentWorkspaces.Take(12).ToList();
        Save();
    }
    public void Remove(string path)
    {
        Data.RecentWorkspaces.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (Data.LastWorkspace?.Equals(path, StringComparison.OrdinalIgnoreCase) == true) Data.LastWorkspace = null;
        Save();
    }
    private void Save() => AtomicFile.Write(SettingsPath, JsonSerializer.Serialize(Data, JsonOptions.Options));
}
