// Cross-platform dev host: the REAL ASP.NET Core MVC backend + a FAKE desktop.
// Lets the React UI be developed and tested against the actual API on any OS.
//   dotnet run --project app/Larp.DevHost -- [--port 5099] [--token dev] [--config-dir <dir>]
using Larp.Core;
using Larp.Web;

string Arg(string name, string fallback)
{
    var i = Array.IndexOf(args, "--" + name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

var port = int.Parse(Arg("port", "5099"));
var token = Arg("token", "dev");
var dir = Arg("config-dir", Path.Combine(Path.GetTempPath(), "larp-devhost"));

// find app/wwwroot by walking up from the binary
var webRoot = Arg("webroot", "");
if (webRoot == "")
{
    for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
    {
        var c = Path.Combine(d.FullName, "app", "wwwroot");
        if (Directory.Exists(c)) { webRoot = c; break; }
        c = Path.Combine(d.FullName, "wwwroot");
        if (Directory.Exists(Path.Combine(c, "ui"))) { webRoot = c; break; }
    }
}
if (webRoot == "") { Console.Error.WriteLine("could not find wwwroot; build the UI (npm run build) or pass --webroot"); return 1; }

var store = ConfigStore.Open(dir);
var desktop = new FakeDesktop();
var app = LarpWeb.Create(new LarpWebOptions(store, desktop, token, webRoot, port), l => l.AddConsole().SetMinimumLevel(LogLevel.Warning));

// test helpers (token-protected like everything else)
app.MapGet("/dev/log", () => desktop.Log.ToArray());
app.MapPost("/dev/clear", () => desktop.Log.Clear());
app.MapPost("/dev/open", (EventBus bus, double x, double y, double w, double h) =>
    bus.Publish("menu:open", new { x, y, w, h }));

app.Start();
Console.WriteLine($"productivity.larp dev host: http://127.0.0.1:{LarpWeb.PortOf(app)}/overlay?t={token}");
Console.WriteLine($"                           http://127.0.0.1:{LarpWeb.PortOf(app)}/settings?t={token}");
Console.WriteLine($"config dir: {store.Dir}");
await app.WaitForShutdownAsync();
return 0;

sealed class FakeDesktop : IDesktop
{
    public System.Collections.Concurrent.ConcurrentQueue<string> Log { get; } = new();
    public Task ApplyTriggerAsync(Config config)
    {
        foreach (var t in config.Triggers) if (t is KeyboardTrigger k) Shortcut.Parse(k.Shortcut); // same validation the Windows shell does
        Log.Enqueue($"trigger:{string.Join("+", config.Triggers.Select(t => t.GetType().Name))}");
        return Task.CompletedTask;
    }
    public Task RunAsync(Prepared action) { Log.Enqueue("run:" + System.Text.Json.JsonSerializer.Serialize(action, action.GetType())); return Task.CompletedTask; }
    public Task OpenSettingsAsync() { Log.Enqueue("settings"); return Task.CompletedTask; }
    public Task CloseOverlayAsync() { Log.Enqueue("close"); return Task.CompletedTask; }
    public Task OverlayReadyAsync() { Log.Enqueue("ready"); return Task.CompletedTask; }
    public Task WarpCursorAsync(double x, double y) { Log.Enqueue($"warp:{Math.Round(x)},{Math.Round(y)}"); return Task.CompletedTask; }
    public Task<string?> PickFileAsync(string? title, IReadOnlyList<string>? extensions) => Task.FromResult<string?>(null);
    public Task OpenConfigDirAsync() { Log.Enqueue("open-config-dir"); return Task.CompletedTask; }
    private bool _autostart;
    public Task<bool> GetAutostartAsync() => Task.FromResult(_autostart);
    public Task SetAutostartAsync(bool enabled) { _autostart = enabled; Log.Enqueue($"autostart:{enabled}"); return Task.CompletedTask; }
}
