using LocalWiki.App.Publishing;

namespace LocalWiki.App.Services;

public sealed class WorkspaceEngine(WorkspaceService workspace, NavigationService navigation, SearchService search, SettingsService settings, StaticPublisherService publisher, ILogger<WorkspaceEngine> logger) : IDisposable
{
    private readonly List<FileSystemWatcher> watchers = [];
    private Timer? debounce;
    public event Action? Changed;
    public string? LastError { get; private set; }
    public async Task OpenAsync(string path)
    {
        StopWatchers();
        workspace.Open(path);
        navigation.Load();
        search.Rebuild();
        settings.Remember(workspace.Root);
        logger.LogInformation("Opened workspace {Workspace}", workspace.Root);
        await PublishSafelyAsync();
        Watch(workspace.Pages, "*.md");
        Watch(workspace.Assets, "*");
        Watch(workspace.Wiki, "navigation.json");
        Changed?.Invoke();
    }
    public void Close() { StopWatchers(); workspace.Close(); Changed?.Invoke(); }
    public async Task PublishSafelyAsync()
    {
        try { await publisher.PublishAsync(); LastError = null; }
        catch (Exception ex) { LastError = "Published docs could not be rebuilt: " + ex.Message; Changed?.Invoke(); }
    }
    public async Task ContentChangedAsync()
    {
        search.Rebuild();
        await PublishSafelyAsync();
        Changed?.Invoke();
    }
    private void Watch(string path, string filter)
    {
        var watcher = new FileSystemWatcher(path, filter) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size, EnableRaisingEvents = true };
        watcher.Changed += OnFileChange; watcher.Created += OnFileChange; watcher.Deleted += OnFileChange; watcher.Renamed += (_, _) => QueueRefresh();
        watchers.Add(watcher);
    }
    private void OnFileChange(object sender, FileSystemEventArgs e) => QueueRefresh();
    private void QueueRefresh()
    {
        debounce?.Dispose();
        debounce = new Timer(async _ =>
        {
            try
            {
                navigation.Load();
                search.Rebuild();
                await PublishSafelyAsync();
                Changed?.Invoke();
                logger.LogInformation("Refreshed after workspace file change");
            }
            catch (Exception ex) { logger.LogWarning(ex, "Could not refresh workspace after file change"); LastError = "External change could not be loaded: " + ex.Message; Changed?.Invoke(); }
        }, null, 500, Timeout.Infinite);
    }
    private void StopWatchers()
    {
        debounce?.Dispose();
        foreach (var watcher in watchers) watcher.Dispose();
        watchers.Clear();
    }
    public void Dispose() => StopWatchers();
}
