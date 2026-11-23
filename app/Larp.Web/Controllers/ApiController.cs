using Larp.Core;
using Microsoft.AspNetCore.Mvc;

namespace Larp.Web.Controllers;

public sealed record ConfigPayload(string Raw, string? Error, string ConfigDir, bool FirstRun);
public sealed record SaveRequest(string Json);
public sealed record WarpRequest(double X, double Y);
public sealed record PickRequest(string? Title, string[]? Extensions);

/// <summary>The JSON API the React UI talks to. Every call is token-protected by <see cref="LocalOnlyMiddleware"/>.</summary>
[ApiController, Route("api")]
public sealed class ApiController(ConfigStore store, IDesktop desktop, EventBus bus) : ControllerBase
{
    [HttpGet("config")]
    public ConfigPayload GetConfig() => new(store.RawText(), store.LoadError, store.Dir, store.FirstRun);

    /// <summary>Validate, register the new trigger, then persist. If the trigger can't be registered the old one stays and nothing is saved.</summary>
    [HttpPut("config")]
    public async Task<IActionResult> SaveConfig([FromBody] SaveRequest req)
    {
        Config next;
        try { next = ConfigParser.Parse(req.Json); }
        catch (LarpException e) { return Problem400(e); }

        var previous = store.Current;
        try { await desktop.ApplyTriggerAsync(next); }
        catch (LarpException e)
        {
            try { await desktop.ApplyTriggerAsync(previous); } catch (LarpException) { /* keep going: report the original error */ }
            return Problem400(e);
        }

        store.Save(req.Json);
        bus.Publish("config:changed");
        return NoContent();
    }

    /// <summary>
    /// Run a menu item by id. The page only says WHICH item was chosen — the action is looked up in
    /// the server's own copy of the config, so a page can never smuggle in a command of its own.
    /// </summary>
    [HttpPost("run/{id}")]
    public async Task<IActionResult> Run(string id)
    {
        try
        {
            var node = store.Current.FindNode(id) ?? throw new LarpException($"unknown menu item `{id}`");
            var action = node.Action ?? throw new LarpException("this item has no action yet");
            var prepared = ActionPreparer.Prepare(action, store.EnvLookup());

            if (prepared is SettingsPrepared) await desktop.OpenSettingsAsync();
            else await desktop.RunAsync(prepared);

            await CloseOverlay();
            return NoContent();
        }
        catch (LarpException e) { return Problem400(e); }
    }

    [HttpPost("overlay/close")]
    public async Task<IActionResult> CloseOverlayRequest() { await CloseOverlay(); return NoContent(); }

    /// <summary>Hide the window (desktop) and tell the page to reset (event).</summary>
    private async Task CloseOverlay()
    {
        await desktop.CloseOverlayAsync();
        bus.Publish("menu:close");
    }

    [HttpPost("overlay/ready")]
    public async Task<IActionResult> OverlayReady() { await desktop.OverlayReadyAsync(); return NoContent(); }

    [HttpPost("cursor")]
    public async Task<IActionResult> Warp([FromBody] WarpRequest r) { await desktop.WarpCursorAsync(r.X, r.Y); return NoContent(); }

    [HttpPost("settings/open")]
    public async Task<IActionResult> OpenSettings() { await desktop.OpenSettingsAsync(); return NoContent(); }

    [HttpPost("config-dir/open")]
    public async Task<IActionResult> OpenConfigDir() { await desktop.OpenConfigDirAsync(); return NoContent(); }

    [HttpPost("pick-file")]
    public async Task<object> PickFile([FromBody] PickRequest r) => new { path = await desktop.PickFileAsync(r.Title, r.Extensions) };

    /// <summary>A local image as a data: URL, for slices that use "image".</summary>
    [HttpGet("image")]
    public IActionResult Image([FromQuery] string path)
    {
        try { return Ok(new { dataUrl = ImageLoader.ToDataUrl(path) }); }
        catch (LarpException e) { return Problem400(e); }
    }

    private BadRequestObjectResult Problem400(LarpException e) => BadRequest(new { error = e.Message });
}
