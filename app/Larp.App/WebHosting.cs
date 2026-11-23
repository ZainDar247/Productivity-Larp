using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Larp.App;

/// <summary>One shared WebView2 environment, so the overlay and settings windows share a single browser process.</summary>
internal static class WebHosting
{
    private static Task<CoreWebView2Environment>? _env;

    public static Task<CoreWebView2Environment> EnvironmentAsync() => _env ??= CoreWebView2Environment.CreateAsync(
        userDataFolder: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "larp.productivity", "webview"));

    /// <summary>Initialise a WebView2 as a locked-down app surface (no devtools, menus, zoom, or browser shortcuts) and open the page.</summary>
    public static async Task InitAsync(WebView2 web, string url)
    {
        await web.EnsureCoreWebView2Async(await EnvironmentAsync());
        var s = web.CoreWebView2.Settings;
        s.AreDevToolsEnabled = false;
        s.AreDefaultContextMenusEnabled = false;
        s.IsZoomControlEnabled = false;
        s.IsStatusBarEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        // The page may only live on our loopback server.
        web.CoreWebView2.NavigationStarting += (_, e) =>
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) || u.Host != "127.0.0.1") e.Cancel = true;
        };
        web.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
        web.CoreWebView2.Navigate(url);
    }

    public const string RuntimeHelp =
        "productivity.larp needs the Microsoft Edge WebView2 Runtime (included in current Windows 10/11).\n" +
        "Download: https://go.microsoft.com/fwlink/p/?LinkId=2124703";
}
