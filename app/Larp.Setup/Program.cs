using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Larp.Setup;

/// <summary>
/// Installs productivity.larp for the current user (no administrator rights): copies the app to
/// %LocalAppData%\Programs, adds Start Menu / Desktop shortcuts, an "Apps and features" uninstall entry and
/// (optionally) start-with-Windows. Run again with --uninstall (or from Apps and features) to remove it.
/// Switches: /S silent, /D=path install folder, --no-launch, --no-desktop, --autostart, --uninstall
/// </summary>
internal static class Program
{
    private const string App = "productivity.larp";
    private const string ExeName = "productivity.larp.exe";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\productivity.larp";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string WebView2Guid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    private static bool _silent;

    [STAThread]
    private static int Main(string[] args)
    {
        _silent = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase) || a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
        var dir = args.Select(a => a.StartsWith("/D=", StringComparison.OrdinalIgnoreCase) ? a[3..].Trim('"') : null).FirstOrDefault(a => a is not null)
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", App);
        try
        {
            return args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase) ? Uninstall(dir, args) : Install(dir, args);
        }
        catch (Exception e)
        {
            Box($"Something went wrong:\n\n{e.Message}", MB_ERROR);
            return 1;
        }
    }

    // ---------------------------------------------------------------- install
    private static int Install(string dir, string[] args)
    {
        var upgrading = Registry.CurrentUser.OpenSubKey(UninstallKey) is not null;
        if (!_silent)
        {
            if (Box($"{(upgrading ? "Update" : "Install")} {App} for your Windows account?\n\nLocation:\n{dir}\n\nNo administrator rights are needed. Your menu and settings are kept.", MB_OKCANCEL | MB_QUESTION) != IDOK)
                return 2;
        }
        var desktop = !args.Contains("--no-desktop") && (_silent || Box("Create a Desktop shortcut?", MB_YESNO | MB_QUESTION) == IDYES);
        var autostart = _silent ? args.Contains("--autostart") : Box("Start productivity.larp automatically when you sign in to Windows?\n\n(It runs quietly in the tray. You can change this later in Settings.)", MB_YESNO | MB_QUESTION) == IDYES;

        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
                            ?? throw new InvalidOperationException("This installer was built without the app payload.");

        StopRunningCopy(dir);
        EnsureWebView2();

        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        using (var zip = new ZipArchive(payload)) zip.ExtractToDirectory(dir, true);

        var exe = Path.Combine(dir, ExeName);
        if (!File.Exists(exe)) throw new FileNotFoundException($"{ExeName} is missing from the installer.");

        // the uninstaller is this same exe, kept next to the app
        var uninstaller = Path.Combine(dir, "uninstall.exe");
        File.Copy(Environment.ProcessPath!, uninstaller, true);

        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), App + ".lnk"), exe);
        var deskLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), App + ".lnk");
        if (desktop) Shortcut(deskLink, exe); else TryDelete(deskLink);

        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (autostart) run.SetValue(App, $"\"{exe}\" --background");
            else if (run.GetValue(App) is string v && v.Contains(exe, StringComparison.OrdinalIgnoreCase)) run.DeleteValue(App, false);
        }

        var size = new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024;
        using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", App);
            k.SetValue("DisplayVersion", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");
            k.SetValue("Publisher", App);
            k.SetValue("InstallLocation", dir);
            k.SetValue("DisplayIcon", exe);
            k.SetValue("UninstallString", $"\"{uninstaller}\" --uninstall");
            k.SetValue("QuietUninstallString", $"\"{uninstaller}\" --uninstall /S");
            k.SetValue("EstimatedSize", (int)size, RegistryValueKind.DWord);
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        var launch = !args.Contains("--no-launch") && !_silent
                     && Box($"{App} is installed.\n\nOpen it now? (Its Settings window opens, and it keeps running in the tray.)", MB_YESNO | MB_INFO) == IDYES;
        if (launch) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = dir });
        return 0;
    }

    // -------------------------------------------------------------- uninstall
    private static int Uninstall(string dir, string[] args)
    {
        if (Registry.CurrentUser.OpenSubKey(UninstallKey)?.GetValue("InstallLocation") is string loc && !args.Any(a => a.StartsWith("/D=", StringComparison.OrdinalIgnoreCase))) dir = loc;
        if (!_silent && Box($"Remove {App} from this computer?", MB_OKCANCEL | MB_QUESTION) != IDOK) return 2;
        var wipeSettings = !_silent && Box("Also delete your menu and settings?\n\n(Choose No to keep them in case you reinstall.)", MB_YESNO | MB_QUESTION | MB_DEFNO) == IDYES;

        StopRunningCopy(dir);
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), App + ".lnk"));
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), App + ".lnk"));
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, true)) run?.DeleteValue(App, false);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        if (wipeSettings)
        {
            try { Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "larp.productivity"), true); }
            catch (IOException) { }
        }

        // this exe lives in the folder being removed: let a tiny helper finish the job once we have exited
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"{dir}\"") { CreateNoWindow = true, UseShellExecute = false });
        if (!_silent) Box($"{App} was removed.", MB_INFO);
        return 0;
    }

    // ---------------------------------------------------------------- helpers
    private static void StopRunningCopy(string dir)
    {
        // only the copy installed in `dir`: never someone's development build
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
        {
            try
            {
                if (p.MainModule?.FileName is { } f && f.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(5000); }
            }
            catch (Exception) { /* access denied or already gone */ }
            finally { p.Dispose(); }
        }
    }

    private static bool HasWebView2()
    {
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            using var b = RegistryKey.OpenBaseKey(hive, view);
            using var k = b.OpenSubKey($@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{WebView2Guid}") ?? b.OpenSubKey($@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{WebView2Guid}");
            if (k?.GetValue("pv") is string v && v != "" && v != "0.0.0.0") return true;
        }
        return false;
    }

    /// <summary>The app draws its windows with Microsoft Edge WebView2 (already part of Windows 11). Fetch the runtime if it is missing.</summary>
    private static void EnsureWebView2()
    {
        if (HasWebView2()) return;
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
            using (var s = http.GetStreamAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703").GetAwaiter().GetResult())
            using (var f = File.Create(tmp)) s.CopyTo(f);
            using var p = Process.Start(new ProcessStartInfo(tmp, "/silent /install") { UseShellExecute = false })!;
            p.WaitForExit();
        }
        catch (Exception e)
        {
            Box($"The Microsoft Edge WebView2 runtime is required but could not be installed automatically ({e.Message}).\n\nInstall it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 if {App} does not open.", MB_INFO);
        }
    }

    private static void Shortcut(string path, string target)
    {
        var t = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Cannot create shortcuts on this system.");
        dynamic sh = Activator.CreateInstance(t)!;
        dynamic lnk = sh.CreateShortcut(path);
        lnk.TargetPath = target;
        lnk.WorkingDirectory = Path.GetDirectoryName(target);
        lnk.IconLocation = target + ",0";
        lnk.Description = "Open the " + App + " settings (starts it if it is not running)";
        lnk.Save();
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { }
    }

    private const uint MB_OKCANCEL = 1, MB_YESNO = 4, MB_ERROR = 0x10, MB_QUESTION = 0x20, MB_INFO = 0x40, MB_DEFNO = 0x100;
    private const int IDOK = 1, IDYES = 6;

    private static int Box(string text, uint type) => _silent ? IDOK : MessageBoxW(0, text, App + " setup", type);
}
