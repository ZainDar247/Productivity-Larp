using Larp.Web;
using Microsoft.Web.WebView2.WinForms;

namespace Larp.App;

/// <summary>
/// The full-screen menu window. It is genuinely see-through (DWM glass + a transparent WebView2), so the live desktop
/// stays visible and un-dimmed around the ring. The window still takes focus and owns the pointer while it is up, so
/// clicks never leak through to the apps beneath and keyboard navigation works.
/// </summary>
internal sealed class OverlayController
{
    private const int Offscreen = -32000;

    private readonly EventBus _bus;
    private OverlayForm? _form;
    private DateTime _openedAt;
    private Point _origin;
    private double _scale = 1;
    private readonly System.Windows.Forms.Timer _revealFallback = new() { Interval = 450 };

    public OverlayController(EventBus bus)
    {
        _bus = bus;
        _revealFallback.Tick += (_, _) => Reveal();
    }

    public bool Visible => _form is { Visible: true };

    /// <summary>Create the hidden window once at startup so opening later is instant.</summary>
    public async Task InitAsync(string pageUrl)
    {
        var form = new OverlayForm();
        _ = form.Handle; // create the HWND without showing
        form.Deactivate += (_, _) =>
        {
            // lost focus to another window: the menu must not linger
            if (Visible && (DateTime.UtcNow - _openedAt).TotalMilliseconds > 500) Close(publish: true);
        };
        await WebHosting.InitAsync(form.Web, pageUrl);
        _form = form;
    }

    public void Toggle() { if (Visible) Close(publish: true); else Open(); }

    public void Open()
    {
        if (_form is null || Visible) return;

        var cursor = Cursor.Position;
        var bounds = Screen.FromPoint(cursor).Bounds; // physical px (PerMonitorV2)
        _scale = Native.ScaleAt(cursor.X, cursor.Y);
        _origin = bounds.Location;
        _openedAt = DateTime.UtcNow;

        // Show it off-screen first: the page can paint its first frame (and tell us so) without a flash of an empty
        // window; Reveal() then moves it onto the monitor.
        _form.Bounds = new Rectangle(Offscreen, Offscreen, bounds.Width, bounds.Height);
        _form.Show();
        Native.ClearFrame(_form.Handle);
        Native.ForceForeground(_form.Handle);
        _form.Web.Focus();

        _bus.Publish("menu:open", new
        {
            x = (cursor.X - bounds.X) / _scale,
            y = (cursor.Y - bounds.Y) / _scale,
            w = bounds.Width / _scale,
            h = bounds.Height / _scale,
        });
        _revealFallback.Start(); // never leave the user with an invisible window if the ack is lost
    }

    public void Reveal()
    {
        _revealFallback.Stop();
        if (_form is { Visible: true } f) { f.Location = _origin; Native.ClearFrame(f.Handle); }
    }

    public void Close(bool publish)
    {
        _revealFallback.Stop();
        if (_form is null) return;
        _form.Hide();
        if (publish) _bus.Publish("menu:close");
    }

    /// <summary>Move the pointer; (x, y) are CSS px inside the overlay.</summary>
    public void Warp(double x, double y) =>
        Cursor.Position = new Point(_origin.X + (int)Math.Round(x * _scale), _origin.Y + (int)Math.Round(y * _scale));
}

internal sealed class OverlayForm : Form
{
    private const int WM_SETTINGCHANGE = 0x001A, WM_DISPLAYCHANGE = 0x007E, WM_THEMECHANGED = 0x031A, WM_DWMCOMPOSITIONCHANGED = 0x031E;

    // Black GDI pixels become transparent once the frame is extended into the client area, and the WebView2
    // background is transparent too — so only what the page draws is visible.
    public WebView2 Web { get; } = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Transparent };

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Text = "productivity.larp overlay";
        Controls.Add(Web);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.ClearFrame(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is WM_DWMCOMPOSITIONCHANGED or WM_THEMECHANGED or WM_SETTINGCHANGE or WM_DISPLAYCHANGE)
            BeginInvoke(() => Native.ClearFrame(Handle)); // put the transparent frame back after Windows re-applied its own
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW: keep it out of Alt-Tab
            return cp;
        }
    }
}
