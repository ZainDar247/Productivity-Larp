using Larp.Core;
using Larp.Web;

namespace Larp.App;

/// <summary>The real <see cref="IDesktop"/>: everything the MVC backend asks of the machine happens here, on the UI thread.</summary>
internal sealed class WindowsDesktop(
    Control ui,
    ConfigStore store,
    Func<TriggerService> triggers,
    Func<OverlayController> overlay,
    Func<SettingsController> settings) : IDesktop
{
    public Task ApplyTriggerAsync(Config config) => OnUi(() => triggers().Apply(config));

    public Task RunAsync(Prepared action) => Task.Run(() => ActionRunner.Run(action));

    public Task OpenSettingsAsync() => OnUi(() => { overlay().Close(publish: false); settings().Open(); });

    public Task CloseOverlayAsync() => OnUi(() => overlay().Close(publish: false)); // the API layer publishes menu:close

    public Task OverlayReadyAsync() => OnUi(() => overlay().Reveal());

    public Task WarpCursorAsync(double x, double y) => OnUi(() => overlay().Warp(x, y));

    public Task OpenConfigDirAsync() => Task.Run(() => ActionRunner.Run(new OpenPrepared(store.Dir)));

    public Task<bool> GetAutostartAsync() => Task.Run(Autostart.IsEnabled);

    public Task SetAutostartAsync(bool enabled) => Task.Run(() => Autostart.Set(enabled));

    public Task<string?> PickFileAsync(string? title, IReadOnlyList<string>? extensions)
    {
        var tcs = new TaskCompletionSource<string?>();
        ui.BeginInvoke(() =>
        {
            try
            {
                using var dlg = new OpenFileDialog { Title = title ?? "Choose a file", CheckFileExists = true };
                if (extensions is { Count: > 0 })
                    dlg.Filter = $"{string.Join(", ", extensions.Select(e => "*." + e))}|{string.Join(";", extensions.Select(e => "*." + e))}|All files|*.*";
                var owner = settings().Form;
                var result = owner is { IsDisposed: false } ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                tcs.SetResult(result == DialogResult.OK ? dlg.FileName : null);
            }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }

    private Task OnUi(Action work)
    {
        var tcs = new TaskCompletionSource();
        ui.BeginInvoke(() =>
        {
            try { work(); tcs.SetResult(); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }
}
