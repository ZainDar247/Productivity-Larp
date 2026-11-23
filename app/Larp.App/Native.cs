using System.Runtime.InteropServices;

namespace Larp.App;

internal static class Native
{
    public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    public const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    public const int WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205;
    public const int WM_MBUTTONDOWN = 0x207, WM_MBUTTONUP = 0x208, WM_XBUTTONDOWN = 0x20B, WM_XBUTTONUP = 0x20C;
    public const uint LLKHF_INJECTED = 0x10, LLMHF_INJECTED = 0x01;
    public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    public delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public nint dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public nint dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)] public static extern nint SetWindowsHookEx(int idHook, HookProc fn, nint hMod, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int nCode, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(nint hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll")] public static extern nint MonitorFromPoint(POINT pt, uint flags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>DPI scale (1.0 = 96 dpi) of the monitor under a physical-pixel point.</summary>
    public static double ScaleAt(int x, int y)
    {
        try
        {
            var mon = MonitorFromPoint(new POINT { X = x, Y = y }, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (mon != 0 && GetDpiForMonitor(mon, 0 /* effective */, out var dx, out _) == 0 && dx > 0) return dx / 96.0;
        }
        catch (Exception) { /* very old Windows: fall through */ }
        return 1.0;
    }

    // ---- see-through window (DWM glass + transparent WebView2), ported from the PSW wheel ----
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct BlurBehind { public int Flags; public bool Enable; public nint Region; public bool TransitionOnMaximized; }
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins m);
    [DllImport("dwmapi.dll")] private static extern int DwmEnableBlurBehindWindow(nint hwnd, ref BlurBehind bb);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint o);

    /// <summary>
    /// Makes the whole window see-through. Safe to call repeatedly — and it has to be: DWM can re-apply its
    /// frame (at logon, on theme / composition changes), which would otherwise leave a dark slab behind the ring.
    /// </summary>
    public static void ClearFrame(nint hwnd)
    {
        if (hwnd == 0) return;
        var all = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref all);

        // an empty blur region makes the glass frame fully transparent (no slab, no blur)
        var empty = CreateRectRgn(0, 0, -1, -1);
        var bb = new BlurBehind { Flags = 0x3 /* ENABLE | BLURREGION */, Enable = true, Region = empty };
        DwmEnableBlurBehindWindow(hwnd, ref bb);
        DeleteObject(empty);

        var none = 1;                                    // DWMSBT_NONE: no Mica/acrylic backdrop
        DwmSetWindowAttribute(hwnd, 38 /* DWMWA_SYSTEMBACKDROP_TYPE */, ref none, sizeof(int));
        var noBorder = unchecked((int)0xFFFFFFFE);       // DWMWA_COLOR_NONE
        DwmSetWindowAttribute(hwnd, 34 /* DWMWA_BORDER_COLOR */, ref noBorder, sizeof(int));
        var square = 1;                                  // DWMWCP_DONOTROUND
        DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref square, sizeof(int));
    }

    public static bool KeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>
    /// A background process may not take the foreground; attaching to the current foreground
    /// thread's input queue is the standard way to get the menu focused for keyboard control.
    /// </summary>
    public static void ForceForeground(nint hwnd)
    {
        var fg = GetForegroundWindow();
        var fgThread = fg == 0 ? 0 : GetWindowThreadProcessId(fg, out _);
        var me = GetCurrentThreadId();
        var attach = fgThread != 0 && fgThread != me;
        if (attach) AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(hwnd);
        BringWindowToTop(hwnd);
        if (attach) AttachThreadInput(me, fgThread, false);
    }
}
