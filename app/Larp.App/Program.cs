using Larp.Core;

namespace Larp.App;

internal static class Program
{
    public const string RestartFlag = "--restart";

    [STAThread]
    private static void Main(string[] args)
    {
        // started by Windows at sign-in: stay quietly in the tray (no Settings window, no first-run popup)
        var background = args.Contains(Autostart.BackgroundFlag, StringComparer.OrdinalIgnoreCase);

        ApplicationConfiguration.Initialize(); // PerMonitorV2 DPI, visual styles

        // a restart waits for the old instance to finish quitting instead of mistaking it for "already running"
        var restarting = args.Contains(RestartFlag, StringComparer.OrdinalIgnoreCase);
        var mutex = new Mutex(true, @"Local\larp.productivity.instance", out var first);
        for (var i = 0; restarting && !first && i < 100; i++)
        {
            mutex.Dispose();
            Thread.Sleep(100);
            mutex = new Mutex(true, @"Local\larp.productivity.instance", out first);
        }
        using var _ = mutex;
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\larp.productivity.show");
        if (!first)
        {
            if (!background) showSignal.Set(); // already running: just bring up its settings
            return;
        }

        var dir = Environment.GetEnvironmentVariable("LARP_CONFIG_DIR")
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "larp.productivity");

        // backups live in Documents (not AppData) so wiping app data or removing the app can't take them too
        var backups = Environment.GetEnvironmentVariable("LARP_CONFIG_DIR") is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "productivity.larp", "config-backups")
            : null;

        ConfigStore store;
        try { store = ConfigStore.Open(dir, backups); }
        catch (Exception e)
        {
            MessageBox.Show($"Could not open the config folder:\n{dir}\n\n{e.Message}", "productivity.larp", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new TrayContext(store, showSignal, background));
    }
}
