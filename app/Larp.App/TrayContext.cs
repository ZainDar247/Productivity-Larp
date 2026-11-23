using Larp.Core;
using Larp.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Larp.App;

/// <summary>Owns the whole app: the MVC backend, the hooks, the overlay, the settings window and the tray icon.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private readonly ConfigStore _store;
    private readonly Control _ui = new();
    private readonly NotifyIcon _tray;
    private readonly WebApplication _web;
    private readonly string _token = LarpWebOptions.NewToken();
    private readonly OverlayController _overlay;
    private readonly SettingsController _settings;
    private readonly TriggerService _triggers;
    private readonly RegisteredWaitHandle _showWait;

    private readonly bool _background;

    public TrayContext(ConfigStore store, EventWaitHandle showSignal, bool background)
    {
        _store = store;
        _background = background;
        _ = _ui.Handle; // a hidden control gives us a UI-thread dispatcher

        using var iconStream = typeof(TrayContext).Assembly.GetManifestResourceStream("icon.ico")!;
        var icon = new Icon(iconStream);

        // forward references: the desktop needs the services that need the web app that needs the desktop
        OverlayController? overlay = null; SettingsController? settings = null; TriggerService? triggers = null;
        var desktop = new WindowsDesktop(_ui, store, () => triggers!, () => overlay!, () => settings!);

        _web = LarpWeb.Create(new LarpWebOptions(store, desktop, _token, Path.Combine(AppContext.BaseDirectory, "wwwroot")));
        _web.Start();
        string Url(string page) => LarpWeb.PageUrl(_web, page, _token);

        overlay = _overlay = new OverlayController(_web.Services.GetRequiredService<EventBus>());
        settings = _settings = new SettingsController(() => Url("settings"), icon);
        triggers = _triggers = new TriggerService(_ui, () => _overlay.Visible, _overlay.Toggle, _overlay.Open);

        _tray = new NotifyIcon
        {
            Icon = icon,
            Text = "productivity.larp",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => _settings.Open();

        // a second launch just asks the running instance to show its settings
        _showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => _ui.BeginInvoke(_settings.Open), null, Timeout.Infinite, false);

        _ = StartAsync(Url("overlay"));
    }

    private async Task StartAsync(string overlayUrl)
    {
        try
        {
            await _overlay.InitAsync(overlayUrl);
        }
        catch (Exception e)
        {
            MessageBox.Show($"The menu window could not start.\n\n{e.Message}\n\n{WebHosting.RuntimeHelp}", "productivity.larp", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitThread();
            return;
        }

        try
        {
            _triggers.Apply(_store.Current);
            if (!_background && (_store.FirstRun || _store.LoadError is not null)) _settings.Open();
        }
        catch (LarpException e)
        {
            // e.g. the shortcut is invalid or the hook couldn't be installed: let the user fix it in Settings
            _tray.ShowBalloonTip(6000, "productivity.larp", $"The trigger isn't active: {e.Message}", ToolTipIcon.Warning);
            if (!_background) _settings.Open();
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => _settings.Open());
        menu.Items.Add("Open config folder", null, (_, _) => ActionRunner.Run(new OpenPrepared(_store.Dir)));
        var startup = new ToolStripMenuItem("Start with Windows") { CheckOnClick = false };
        startup.Click += (_, _) =>
        {
            try { Autostart.Set(!Autostart.IsEnabled()); }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                _tray.ShowBalloonTip(5000, "productivity.larp", $"Couldn't change the startup setting: {e.Message}", ToolTipIcon.Warning);
            }
        };
        menu.Opening += (_, _) => startup.Checked = Autostart.IsEnabled(); // may have been changed from Settings
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        return menu;
    }

    protected override void ExitThreadCore()
    {
        _showWait.Unregister(null);
        _triggers.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        try { _web.StopAsync().Wait(TimeSpan.FromSeconds(2)); } catch (Exception) { /* exiting anyway */ }
        base.ExitThreadCore();
    }
}
