using Larp.Core;

namespace Larp.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // started by Windows at sign-in: stay quietly in the tray (no Settings window, no first-run popup)
        var background = args.Contains(Autostart.BackgroundFlag, StringComparer.OrdinalIgnoreCase);

        ApplicationConfiguration.Initialize(); // PerMonitorV2 DPI, visual styles

        using var mutex = new Mutex(true, @"Local\larp.productivity.instance", out var first);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\larp.productivity.show");
        if (!first)
        {
            if (!background) showSignal.Set(); // already running: just bring up its settings
            return;
        }

        var dir = Environment.GetEnvironmentVariable("LARP_CONFIG_DIR")
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "larp.productivity");

        ConfigStore store;
        try { store = ConfigStore.Open(dir); }
        catch (Exception e)
        {
            MessageBox.Show($"Could not open the config folder:\n{dir}\n\n{e.Message}", "productivity.larp", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new TrayContext(store, showSignal, background));
    }
}
