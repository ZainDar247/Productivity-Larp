using Larp.Core;
using Shortcut = Larp.Core.Shortcut;

namespace Larp.App;

/// <summary>
/// Opens the menu from a keyboard shortcut or mouse button using low-level hooks. Unlike a plain
/// hotkey, a hook sees key-up (so "hold" works) and can swallow the trigger so the app underneath
/// never receives it. Hooks run on the UI thread; handlers return immediately and defer the work.
/// </summary>
internal sealed class TriggerService : IDisposable
{
    private readonly Control _ui;
    private readonly Func<bool> _menuVisible;
    private readonly Action _toggle;
    private readonly Action _open;

    /// <summary>One configured trigger with its own press/hold state, so several can be active at once.</summary>
    private sealed class Binding(Shortcut? shortcut, MouseButton? button, TriggerMachine machine, int holdMs)
    {
        public readonly Shortcut? Shortcut = shortcut;
        public readonly MouseButton? Button = button;
        public readonly TriggerMachine Machine = machine;
        public readonly int HoldMs = holdMs;
        public bool SwallowedDown;
    }

    private nint _kbHook, _mouseHook;
    private readonly Native.HookProc _kbProc, _mouseProc; // fields keep the delegates alive
    private Binding[] _bindings = [];

    public TriggerService(Control ui, Func<bool> menuVisible, Action toggle, Action open)
    {
        _ui = ui; _menuVisible = menuVisible; _toggle = toggle; _open = open;
        _kbProc = KeyboardProc;
        _mouseProc = MouseProc;
    }

    /// <summary>(Re)configure. Parses first, so an invalid shortcut leaves the current triggers untouched. UI thread only.</summary>
    public void Apply(Config cfg)
    {
        var next = new List<Binding>();
        foreach (var t in cfg.Triggers)
        {
            switch (t)
            {
                case KeyboardTrigger k: next.Add(new Binding(Shortcut.Parse(k.Shortcut), null, new TriggerMachine(k.Mode), k.HoldMs)); break;
                case MouseTrigger m: next.Add(new Binding(null, m.Button, new TriggerMachine(m.Mode), m.HoldMs)); break;
            }
        }

        Uninstall();
        _bindings = [.. next];

        if (_bindings.Any(x => x.Shortcut is not null)) _kbHook = Install(Native.WH_KEYBOARD_LL, _kbProc, "keyboard");
        if (_bindings.Any(x => x.Button is not null)) _mouseHook = Install(Native.WH_MOUSE_LL, _mouseProc, "mouse");
    }

    private static nint Install(int id, Native.HookProc proc, string what)
    {
        var h = Native.SetWindowsHookEx(id, proc, Native.GetModuleHandle(null), 0);
        return h != 0 ? h : throw new LarpException($"could not install the {what} hook (Windows error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
    }

    private void Uninstall()
    {
        if (_kbHook != 0) { Native.UnhookWindowsHookEx(_kbHook); _kbHook = 0; }
        if (_mouseHook != 0) { Native.UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
    }

    // ---------------- keyboard ----------------
    private nint KeyboardProc(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= 0)
            {
                var k = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                if ((k.flags & Native.LLKHF_INJECTED) == 0)
                {
                    var msg = (int)wParam;
                    var down = msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                    var up = msg is Native.WM_KEYUP or Native.WM_SYSKEYUP;
                    foreach (var b in _bindings)
                    {
                        if (b.Shortcut is not { } sc || k.vkCode != sc.Vk) continue;
                        if (down && (b.SwallowedDown || ModifiersMatch(sc)))
                        {
                            b.SwallowedDown = true;
                            Dispatch(b, b.Machine.Down()); // auto-repeat returns None
                            return 1;                      // swallow: the app underneath never sees Ctrl+Shift+Z
                        }
                        if (up && b.SwallowedDown)
                        {
                            b.SwallowedDown = false;
                            Dispatch(b, b.Machine.Up());
                            return 1;
                        }
                    }
                }
            }
        }
        catch (Exception) { /* a hook must never throw into Windows */ }
        return Native.CallNextHookEx(_kbHook, code, wParam, lParam);
    }

    private static bool ModifiersMatch(Shortcut s) =>
        Native.KeyDown(Native.VK_CONTROL) == s.Ctrl &&
        Native.KeyDown(Native.VK_SHIFT) == s.Shift &&
        Native.KeyDown(Native.VK_MENU) == s.Alt &&
        (Native.KeyDown(Native.VK_LWIN) || Native.KeyDown(Native.VK_RWIN)) == s.Win;

    // ---------------- mouse ----------------
    private nint MouseProc(int code, nint wParam, nint lParam)
    {
        try
        {
            var msg = (int)wParam;
            // cheap early-out: this hook sees every mouse move
            if (code >= 0 && msg is >= Native.WM_LBUTTONDOWN and <= Native.WM_XBUTTONUP && msg != 0x200)
            {
                var m = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                if ((m.flags & Native.LLMHF_INJECTED) == 0 && Classify(msg, m.mouseData) is { } hit)
                {
                    foreach (var b in _bindings)
                    {
                        if (b.Button is not { } want || hit.Button != want) continue;
                        // While the menu is open, left/right clicks belong to the menu (choose a slice, close).
                        if (_menuVisible() && want is MouseButton.Left or MouseButton.Right) continue;

                        if (hit.Down) { b.SwallowedDown = true; Dispatch(b, b.Machine.Down()); return 1; }
                        if (b.SwallowedDown) { b.SwallowedDown = false; Dispatch(b, b.Machine.Up()); return 1; }
                    }
                }
            }
        }
        catch (Exception) { /* never throw into Windows */ }
        return Native.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private static (MouseButton Button, bool Down)? Classify(int msg, uint mouseData)
    {
        var x = (int)(mouseData >> 16) == 1 ? MouseButton.Back : MouseButton.Forward;
        return msg switch
        {
            Native.WM_LBUTTONDOWN => (MouseButton.Left, true),
            Native.WM_LBUTTONUP => (MouseButton.Left, false),
            Native.WM_RBUTTONDOWN => (MouseButton.Right, true),
            Native.WM_RBUTTONUP => (MouseButton.Right, false),
            Native.WM_MBUTTONDOWN => (MouseButton.Middle, true),
            Native.WM_MBUTTONUP => (MouseButton.Middle, false),
            Native.WM_XBUTTONDOWN => (x, true),
            Native.WM_XBUTTONUP => (x, false),
            _ => null,
        };
    }

    // ---------------- outcome ----------------
    private void Dispatch(Binding b, TriggerOutcome outcome)
    {
        switch (outcome)
        {
            case TriggerOutcome.Toggle:
                _ui.BeginInvoke(_toggle);
                break;
            case TriggerOutcome.StartHoldTimer:
                var generation = b.Machine.Generation;
                _ = Task.Delay(b.HoldMs).ContinueWith(_ => _ui.BeginInvoke(() =>
                {
                    if (_bindings.Contains(b) && b.Machine.HoldElapsed(generation, _menuVisible()) == TriggerOutcome.Open)
                        _open();
                }));
                break;
        }
    }

    public void Dispose() => Uninstall();
}
