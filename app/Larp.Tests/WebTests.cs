using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Larp.Core;
using Larp.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Larp.Tests;

/// <summary>Spins up the REAL ASP.NET Core MVC backend on a loopback port with a fake desktop.</summary>
public sealed class WebFixture : IAsyncLifetime
{
    public const string Token = "test-token-123";
    public TempDir Dir { get; } = new();
    public FakeDesktop Desktop { get; } = new();
    public ConfigStore Store { get; private set; } = null!;
    public WebApplication App { get; private set; } = null!;
    public HttpClient Http { get; private set; } = null!;
    public string BaseUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        Store = ConfigStore.Open(Path.Combine(Dir.Path, "cfg"));
        var webRoot = Path.Combine(Dir.Path, "wwwroot");
        Directory.CreateDirectory(Path.Combine(webRoot, "ui"));
        File.WriteAllText(Path.Combine(webRoot, "ui", "app.js"), "// js");
        File.WriteAllText(Path.Combine(webRoot, "ui", "app.css"), "/* css */");

        App = LarpWeb.Create(new LarpWebOptions(Store, Desktop, Token, webRoot));
        await App.StartAsync();
        BaseUrl = $"http://127.0.0.1:{LarpWeb.PortOf(App)}";
        Http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    /// <summary>Back to the starter menu — call before each test so tests can't affect each other.</summary>
    public void Reset()
    {
        Store.Save(DefaultConfig.Json);
        Desktop.Log.Clear(); Desktop.Ran.Clear(); Desktop.RejectTriggers = false;
        Environment.SetEnvironmentVariable("RDP_DIR", null);
    }

    public HttpRequestMessage Req(HttpMethod m, string path, object? body = null, bool auth = true)
    {
        var r = new HttpRequestMessage(m, path);
        if (auth) r.Headers.Add(LarpWeb.TokenHeader, Token);
        if (body is not null) r.Content = JsonContent.Create(body);
        return r;
    }

    public async Task DisposeAsync() { await App.StopAsync(); await App.DisposeAsync(); Dir.Dispose(); }
}

public sealed class FakeDesktop : IDesktop
{
    public List<string> Log { get; } = [];
    public List<Prepared> Ran { get; } = [];
    public bool RejectTriggers { get; set; }

    public Task ApplyTriggerAsync(Config c)
    {
        if (RejectTriggers) throw new LarpException("shortcut already taken");
        Log.Add("trigger"); return Task.CompletedTask;
    }
    public Task RunAsync(Prepared a) { Ran.Add(a); Log.Add("run"); return Task.CompletedTask; }
    public Task OpenSettingsAsync() { Log.Add("settings"); return Task.CompletedTask; }
    public Task CloseOverlayAsync() { Log.Add("close"); return Task.CompletedTask; }
    public Task OverlayReadyAsync() { Log.Add("ready"); return Task.CompletedTask; }
    public Task WarpCursorAsync(double x, double y) { Log.Add($"warp:{x},{y}"); return Task.CompletedTask; }
    public Task<string?> PickFileAsync(string? t, IReadOnlyList<string>? e) => Task.FromResult<string?>(@"C:\picked.rdp");
    public Task OpenConfigDirAsync() { Log.Add("dir"); return Task.CompletedTask; }
}

public class WebSecurityTests(WebFixture f) : IClassFixture<WebFixture>
{
    private Task<HttpResponseMessage> Send(HttpRequestMessage r) => f.Http.SendAsync(r);

    [Fact] public async Task Api_without_token_is_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(f.Req(HttpMethod.Get, "/api/config", auth: false))).StatusCode);

    [Fact] public async Task Api_with_wrong_token_is_401()
    {
        var r = new HttpRequestMessage(HttpMethod.Get, "/api/config");
        r.Headers.Add(LarpWeb.TokenHeader, "nope");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(r)).StatusCode);
    }

    [Fact] public async Task Api_with_token_is_200() =>
        Assert.Equal(HttpStatusCode.OK, (await Send(f.Req(HttpMethod.Get, "/api/config"))).StatusCode);

    [Fact] public async Task Pages_need_the_token_too()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Http.GetAsync("/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Http.GetAsync("/overlay")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Http.GetAsync($"/settings?t={WebFixture.Token}")).StatusCode);
    }

    [Fact] public async Task Rebinding_style_Host_header_is_refused()
    {
        var r = f.Req(HttpMethod.Get, "/api/config");
        r.Headers.Host = "evil.example.com";
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(r)).StatusCode);
    }

    [Fact] public async Task Static_bundle_is_public_but_nothing_else_is()
    {
        Assert.Equal(HttpStatusCode.OK, (await f.Http.GetAsync("/ui/app.js")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Http.GetAsync("/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Http.GetAsync("/shot/1")).StatusCode);
    }

    [Fact] public async Task Server_binds_loopback_only() =>
        Assert.StartsWith("http://127.0.0.1:", f.BaseUrl);

    [Fact] public async Task Page_embeds_the_token_and_the_view_name()
    {
        var html = await f.Http.GetStringAsync($"/overlay?t={WebFixture.Token}");
        Assert.Contains("data-view=\"overlay\"", html);
        Assert.Contains($"window.__LARP_TOKEN = \"{WebFixture.Token}\"", html);
        Assert.Contains("/ui/app.js", html);
    }
}

public class WebApiTests : IClassFixture<WebFixture>
{
    private readonly WebFixture f;
    public WebApiTests(WebFixture fixture) { f = fixture; f.Reset(); } // xUnit builds a new instance per test

    private async Task<(HttpStatusCode, JsonElement)> Do(HttpMethod m, string path, object? body = null)
    {
        var res = await f.Http.SendAsync(f.Req(m, path, body));
        var text = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, text.Length > 0 ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }

    [Fact] public async Task Run_resolves_the_action_server_side_and_closes_the_overlay()
    {
        Environment.SetEnvironmentVariable("RDP_DIR", @"D:\rdp");
        try
        {
            f.Desktop.Log.Clear(); f.Desktop.Ran.Clear();
            var (code, _) = await Do(HttpMethod.Post, "/api/run/rdp-admin-1");
            Assert.Equal(HttpStatusCode.NoContent, code);
            var p = Assert.IsType<SpawnPrepared>(Assert.Single(f.Desktop.Ran));
            Assert.Equal("mstsc.exe", p.Program);
            Assert.Equal([@"D:\rdp\admin\server1.rdp"], p.Args);
            Assert.Contains("close", f.Desktop.Log);
        }
        finally { Environment.SetEnvironmentVariable("RDP_DIR", null); }
    }

    [Fact] public async Task Unknown_item_is_a_400_with_a_message_and_launches_nothing()
    {
        f.Desktop.Ran.Clear();
        var (code, body) = await Do(HttpMethod.Post, "/api/run/not-a-real-id");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("unknown menu item", body.GetProperty("error").GetString());
        Assert.Empty(f.Desktop.Ran);
    }

    [Fact] public async Task A_folder_has_no_action_to_run()
    {
        var (code, body) = await Do(HttpMethod.Post, "/api/run/rdp");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("no action", body.GetProperty("error").GetString());
    }

    [Fact] public async Task Missing_env_var_is_reported_and_nothing_runs()
    {
        Environment.SetEnvironmentVariable("RDP_DIR", null);
        f.Desktop.Ran.Clear();
        var (code, body) = await Do(HttpMethod.Post, "/api/run/rdp-admin-1");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("RDP_DIR", body.GetProperty("error").GetString());
        Assert.Empty(f.Desktop.Ran);
    }

    [Fact] public async Task Settings_item_opens_settings_instead_of_running_anything()
    {
        f.Desktop.Log.Clear(); f.Desktop.Ran.Clear();
        var (code, _) = await Do(HttpMethod.Post, "/api/run/settings");
        Assert.Equal(HttpStatusCode.NoContent, code);
        Assert.Contains("settings", f.Desktop.Log);
        Assert.Empty(f.Desktop.Ran);
    }

    [Fact] public async Task Save_persists_json_verbatim_registers_trigger_and_announces_the_change()
    {
        using var sse = await OpenSseAsync();
        const string json = """{"trigger":{"type":"keyboard","shortcut":"Ctrl+Alt+K"},"theme":{"accent":"#123456"},"menu":[{"id":"only","label":"Only"}]}""";
        f.Desktop.Log.Clear();
        var (code, _) = await Do(HttpMethod.Put, "/api/config", new { json });
        Assert.Equal(HttpStatusCode.NoContent, code);
        Assert.Equal(json, File.ReadAllText(f.Store.ConfigPath));
        Assert.Contains("trigger", f.Desktop.Log);
        Assert.Contains("event: config:changed", await ReadUntilAsync(sse, "config:changed"));
    }

    [Fact] public async Task A_trigger_the_desktop_rejects_means_400_and_nothing_is_saved()
    {
        var before = File.ReadAllText(f.Store.ConfigPath);
        f.Desktop.RejectTriggers = true;
        try
        {
            var (code, body) = await Do(HttpMethod.Put, "/api/config", new { json = """{"trigger":{"type":"keyboard","shortcut":"F2"},"menu":[]}""" });
            Assert.Equal(HttpStatusCode.BadRequest, code);
            Assert.Contains("already taken", body.GetProperty("error").GetString());
            Assert.Equal(before, File.ReadAllText(f.Store.ConfigPath));
        }
        finally { f.Desktop.RejectTriggers = false; }
    }

    [Fact] public async Task Invalid_config_is_refused_before_the_trigger_is_touched()
    {
        f.Desktop.Log.Clear();
        var (code, _) = await Do(HttpMethod.Put, "/api/config", new { json = """{"trigger":{"type":"keyboard","shortcut":"F1"},"menu":[{"id":"a"},{"id":"a"}]}""" });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.DoesNotContain("trigger", f.Desktop.Log);
    }

    [Fact] public async Task Close_and_ready_and_warp_reach_the_desktop()
    {
        f.Desktop.Log.Clear();
        await Do(HttpMethod.Post, "/api/overlay/ready");
        await Do(HttpMethod.Post, "/api/cursor", new { x = 640.5, y = 400 });
        await Do(HttpMethod.Post, "/api/overlay/close");
        Assert.Equal(["ready", "warp:640.5,400", "close"], f.Desktop.Log);
    }

    [Fact] public async Task Close_tells_every_page_to_reset_via_SSE()
    {
        using var sse = await OpenSseAsync();
        await Do(HttpMethod.Post, "/api/overlay/close");
        Assert.Contains("event: menu:close", await ReadUntilAsync(sse, "menu:close"));
    }

    [Fact] public async Task File_picker_result_is_returned()
    {
        var (_, body) = await Do(HttpMethod.Post, "/api/pick-file", new { title = "t", extensions = new[] { "rdp" } });
        Assert.Equal(@"C:\picked.rdp", body.GetProperty("path").GetString());
    }

    [Fact] public async Task Image_endpoint_returns_a_data_url_and_rejects_non_images()
    {
        var png = Path.Combine(f.Dir.Path, "i.png");
        File.WriteAllBytes(png, [1, 2, 3]);
        var (ok, body) = await Do(HttpMethod.Get, $"/api/image?path={Uri.EscapeDataString(png)}");
        Assert.Equal(HttpStatusCode.OK, ok);
        Assert.Equal("data:image/png;base64,AQID", body.GetProperty("dataUrl").GetString());
        var (bad, _) = await Do(HttpMethod.Get, $"/api/image?path={Uri.EscapeDataString(Path.Combine(f.Dir.Path, "cfg", "config.json"))}");
        Assert.Equal(HttpStatusCode.BadRequest, bad); // a config/secret file is not an image
    }

    [Fact] public async Task Screenshot_backdrop_is_served_with_the_token_and_not_cached()
    {
        var id = f.App.Services.GetService(typeof(ShotStore)) is ShotStore s ? s.Put([0xFF, 0xD8, 0xFF]) : 0;
        var res = await f.Http.SendAsync(f.Req(HttpMethod.Get, $"/shot/{id}"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/jpeg", res.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", res.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Http.SendAsync(f.Req(HttpMethod.Get, "/shot/9999"))).StatusCode);
    }

    // ---- SSE helpers ----
    private async Task<Stream> OpenSseAsync()
    {
        var res = await f.Http.SendAsync(f.Req(HttpMethod.Get, "/events"), HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal("text/event-stream", res.Content.Headers.ContentType?.MediaType);
        var s = await res.Content.ReadAsStreamAsync();
        await ReadUntilAsync(s, ": connected"); // the server is now subscribed
        return s;
    }

    private static async Task<string> ReadUntilAsync(Stream s, string needle)
    {
        var sb = new StringBuilder();
        var buf = new byte[1024];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!sb.ToString().Contains(needle))
        {
            var n = await s.ReadAsync(buf, cts.Token);
            if (n == 0) break;
            sb.Append(Encoding.UTF8.GetString(buf, 0, n));
        }
        return sb.ToString();
    }
}
