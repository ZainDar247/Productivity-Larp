using Microsoft.Win32;

namespace Larp.App;

/// <summary>
/// "Start with Windows": a per-user Run-key entry (no admin rights needed). The app is started with
/// <see cref="BackgroundFlag"/> so it comes up silently in the tray instead of opening Settings.
/// </summary>
internal static class Autostart
{
    public const string BackgroundFlag = "--background";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "productivity.larp";

    private static string Command => $"\"{Environment.ProcessPath}\" {BackgroundFlag}";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string v && v.Contains(Environment.ProcessPath ?? "?", StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
