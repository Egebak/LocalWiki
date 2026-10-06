using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using LocalWiki.App.Components;
using LocalWiki.App.Publishing;
using LocalWiki.App.Services;

var options = args.ToList();
string? Value(string name) { var i = options.IndexOf(name); return i >= 0 && i + 1 < options.Count ? options[i + 1] : null; }
var workspacePath = Value("--workspace");
var publishOnly = options.Contains("--publish-only");
var noBrowser = options.Contains("--no-browser") || publishOnly;
var portText = Value("--port");
if (publishOnly && string.IsNullOrWhiteSpace(workspacePath)) { Console.Error.WriteLine("--publish-only requires --workspace."); return 2; }
if (portText is not null && (!int.TryParse(portText, out var parsed) || parsed is < 1 or > 65535)) { Console.Error.WriteLine("Invalid port."); return 2; }
var port = portText is null ? 0 : int.Parse(portText);

var webOptions = File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "app.css"))
    ? new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory }
    : new WebApplicationOptions { Args = args };
var builder = WebApplication.CreateBuilder(webOptions);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddHubOptions(o => o.MaximumReceiveMessageSize = 15 * 1024 * 1024);
builder.Services.AddSingleton<WorkspaceService>();
builder.Services.AddSingleton<NavigationService>();
builder.Services.AddSingleton<MarkdownFileService>();
builder.Services.AddSingleton<WikiLinkService>();
builder.Services.AddSingleton<MarkdownRenderService>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<AssetService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<WorkspaceBackgroundService>();
builder.Services.AddSingleton<StaticPublisherService>();
builder.Services.AddSingleton<WorkspaceEngine>();
var app = builder.Build();
if (!string.IsNullOrWhiteSpace(workspacePath)) await app.Services.GetRequiredService<WorkspaceEngine>().OpenAsync(workspacePath);
if (publishOnly) return app.Services.GetRequiredService<WorkspaceEngine>().LastError is null ? 0 : 1;

app.UseStaticFiles();
app.UseAntiforgery();
app.MapGet("/workspace-assets/{name}", (string name, WorkspaceService ws, AssetService assets) =>
{
    if (!ws.IsOpen) return Results.NotFound();
    var file = assets.Resolve(name);
    return file is null ? Results.NotFound() : Results.File(file.Value.Path, file.Value.Type);
});
app.MapGet("/app-background/{id}", (string id, WorkspaceBackgroundService backgrounds) =>
{
    var image = backgrounds.ResolveBackground(id);
    return image is null ? Results.NotFound() : Results.File(image.Value.Path, image.Value.ContentType);
});
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
await app.StartAsync();
var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
var address = addresses?.FirstOrDefault() ?? app.Urls.First();
app.Logger.LogInformation("LocalWiki listening at {Url}", address);
if (!noBrowser && OperatingSystem.IsWindows())
{
    try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Could not open default browser"); }
}
await app.WaitForShutdownAsync();
return 0;
