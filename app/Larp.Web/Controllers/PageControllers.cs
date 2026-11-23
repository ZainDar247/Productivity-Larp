using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Larp.Web.Controllers;

/// <summary>Razor views that host the React bundle. One bundle, two windows: <c>/overlay</c> and <c>/settings</c>.</summary>
public sealed class HomeController : Controller
{
    [HttpGet("overlay")]
    public IActionResult Overlay() => Page("overlay");

    [HttpGet("settings")]
    public IActionResult Settings() => Page("settings");

    private IActionResult Page(string name)
    {
        ViewData["View"] = name;
        ViewData["Token"] = (string?)Request.Query["t"] ?? "";
        Response.Headers.CacheControl = "no-store";
        return View(name);
    }
}

/// <summary>Server-sent events: how the desktop shell tells the pages "open the menu" / "close" / "config changed".</summary>
public sealed class EventsController(EventBus bus) : ControllerBase
{
    [HttpGet("/events")]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store";
        await Write(": connected\n\n", ct);

        using var sub = bus.Subscribe();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
                wait.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    var m = await sub.Reader.ReadAsync(wait.Token);
                    await Write($"event: {m.Event}\ndata: {m.DataJson}\n\n", ct);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    await Write(": ping\n\n", ct); // keep-alive
                }
            }
        }
        catch (OperationCanceledException) { /* client went away */ }
    }

    private async Task Write(string s, CancellationToken ct)
    {
        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(s), ct);
        await Response.Body.FlushAsync(ct);
    }
}

/// <summary>The frozen desktop picture drawn behind the ring.</summary>
public sealed class ShotController(ShotStore shots) : ControllerBase
{
    [HttpGet("/shot/{id:int}")]
    public IActionResult Get(int id)
    {
        var bytes = shots.Get(id);
        if (bytes is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(bytes, "image/jpeg");
    }
}
