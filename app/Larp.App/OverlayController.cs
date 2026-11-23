using System.Drawing.Imaging;
using Larp.Web;
using Microsoft.Web.WebView2.WinForms;

namespace Larp.App;

/// <summary>
/// The full-screen menu window. Instead of fighting WebView2 for per-pixel transparency, it freezes
/// a screenshot of the monitor under the cursor and shows that as the backdrop — so the menu looks
/// transparent on every Windows version, and clicks can never leak through to the apps beneath.
/// </summary>
internal sealed class OverlayController
{
    private readonly EventBus _bus;
    private readonly ShotStore _shots;
    private OverlayForm? _form;
    private DateTime _openedAt;
    private Point _origin;
    private double _scale = 1;
    private readonly System.Windows.Forms.Timer _revealFallback = new() { Interval = 450 };

    public OverlayController(EventBus bus, ShotStore shots)
    {
        _bus = bus; _shots = shots;
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

        var shot = _shots.Put(CaptureJpeg(bounds)); // capture BEFORE the overlay appears
        _openedAt = DateTime.UtcNow;

        _form.Opacity = 0; // stays invisible until the page reports its first frame
        _form.Bounds = bounds;
        _form.Show();
        Native.ForceForeground(_form.Handle);
        _form.Web.Focus();

        _bus.Publish("menu:open", new
        {
            x = (cursor.X - bounds.X) / _scale,
            y = (cursor.Y - bounds.Y) / _scale,
            w = bounds.Width / _scale,
            h = bounds.Height / _scale,
            shot = $"/shot/{shot}",
        });
        _revealFallback.Start(); // never leave the user with an invisible window if the ack is lost
    }

    public void Reveal()
    {
        _revealFallback.Stop();
        if (_form is { Visible: true }) _form.Opacity = 1;
    }

    public void Close(bool publish)
    {
        _revealFallback.Stop();
        if (_form is null) return;
        _form.Hide();
        _form.Opacity = 0;
        if (publish) _bus.Publish("menu:close");
    }

    /// <summary>Move the pointer; (x, y) are CSS px inside the overlay.</summary>
    public void Warp(double x, double y) =>
        Cursor.Position = new Point(_origin.X + (int)Math.Round(x * _scale), _origin.Y + (int)Math.Round(y * _scale));

    private static byte[] CaptureJpeg(Rectangle r)
    {
        using var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);

        var jpeg = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
        using var ps = new EncoderParameters(1);
        ps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
        using var ms = new MemoryStream();
        bmp.Save(ms, jpeg, ps);
        return ms.ToArray();
    }
}

internal sealed class OverlayForm : Form
{
    public WebView2 Web { get; } = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Opacity = 0;
        Text = "productivity.larp overlay";
        Controls.Add(Web);
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
