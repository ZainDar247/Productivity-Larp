using Microsoft.Web.WebView2.WinForms;

namespace Larp.App;

/// <summary>The settings window. Created on demand and disposed when closed, so it costs no memory while you're not using it.</summary>
internal sealed class SettingsController(Func<string> pageUrl, Icon icon)
{
    private Form? _form;

    public Form? Form => _form;

    public void Open()
    {
        if (_form is { IsDisposed: false })
        {
            if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
            _form.Show();
            Raise(_form);
            return;
        }

        var web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(15, 17, 23) };
        var form = new Form
        {
            Text = "productivity.larp — Settings",
            Icon = icon,
            ClientSize = new Size(1180, 780),
            MinimumSize = new Size(900, 600),
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.FromArgb(15, 17, 23),
        };
        form.Controls.Add(web);
        form.FormClosed += (_, _) => { web.Dispose(); _form = null; };
        _form = form;
        form.Show();
        Raise(form);

        _ = Init(web, form);
    }

    /// <summary>Windows often refuses to steal focus for a background process; flipping TopMost reliably puts the window in front.</summary>
    private static void Raise(Form f)
    {
        f.TopMost = true;
        f.TopMost = false;
        f.Activate();
    }

    private async Task Init(WebView2 web, Form form)
    {
        try { await WebHosting.InitAsync(web, pageUrl()); }
        catch (Exception e)
        {
            MessageBox.Show(form, $"The settings window could not start.\n\n{e.Message}\n\n{WebHosting.RuntimeHelp}", "productivity.larp", MessageBoxButtons.OK, MessageBoxIcon.Error);
            form.Close();
        }
    }
}
