namespace Larp.Core;

/// <summary>A parsed keyboard shortcut. <see cref="Vk"/> is a Windows virtual-key code.</summary>
public sealed record Shortcut(bool Ctrl, bool Shift, bool Alt, bool Win, int Vk)
{
    private static readonly Dictionary<string, int> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["space"] = 0x20, ["enter"] = 0x0D, ["return"] = 0x0D, ["tab"] = 0x09, ["escape"] = 0x1B, ["esc"] = 0x1B,
        ["backspace"] = 0x08, ["delete"] = 0x2E, ["del"] = 0x2E, ["insert"] = 0x2D, ["ins"] = 0x2D,
        ["home"] = 0x24, ["end"] = 0x23, ["pageup"] = 0x21, ["pagedown"] = 0x22,
        ["arrowup"] = 0x26, ["up"] = 0x26, ["arrowdown"] = 0x28, ["down"] = 0x28,
        ["arrowleft"] = 0x25, ["left"] = 0x25, ["arrowright"] = 0x27, ["right"] = 0x27,
        ["backquote"] = 0xC0, ["`"] = 0xC0, ["grave"] = 0xC0, ["minus"] = 0xBD, ["-"] = 0xBD,
        ["equal"] = 0xBB, ["="] = 0xBB, ["bracketleft"] = 0xDB, ["["] = 0xDB, ["bracketright"] = 0xDD, ["]"] = 0xDD,
        ["backslash"] = 0xDC, ["\\"] = 0xDC, ["semicolon"] = 0xBA, [";"] = 0xBA, ["quote"] = 0xDE, ["'"] = 0xDE,
        ["comma"] = 0xBC, [","] = 0xBC, ["period"] = 0xBE, ["."] = 0xBE, ["slash"] = 0xBF, ["/"] = 0xBF,
        ["capslock"] = 0x14, ["printscreen"] = 0x2C, ["pause"] = 0x13,
    };

    /// <summary>Parse e.g. "Ctrl+Shift+Z", "Alt+F5", "Win+Space". Throws <see cref="ConfigException"/> if unusable.</summary>
    public static Shortcut Parse(string text)
    {
        bool ctrl = false, shift = false, alt = false, win = false;
        int? vk = null;

        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            if (raw.Length == 0) continue;
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control" or "commandorcontrol" or "cmdorctrl": ctrl = true; continue;
                case "shift": shift = true; continue;
                case "alt" or "option": alt = true; continue;
                case "win" or "super" or "meta" or "cmd" or "command": win = true; continue;
            }

            if (vk is not null) throw new ConfigException($"shortcut `{text}` has more than one non-modifier key");
            vk = KeyCode(raw) ?? throw new ConfigException($"shortcut `{text}`: unknown key `{raw}`");
        }

        return vk is null
            ? throw new ConfigException($"shortcut `{text}` needs a key besides the modifiers")
            : new Shortcut(ctrl, shift, alt, win, vk.Value);
    }

    private static int? KeyCode(string name)
    {
        if (name.Length == 1 && char.IsAsciiLetter(name[0])) return char.ToUpperInvariant(name[0]);
        if (name.Length == 1 && char.IsAsciiDigit(name[0])) return name[0];
        if (name.Length >= 2 && name[0] is 'F' or 'f' && int.TryParse(name.AsSpan(1), out var f) && f is >= 1 and <= 24) return 0x70 + f - 1;
        if (name.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(6), out var n) && n is >= 0 and <= 9) return 0x60 + n;
        if (name.StartsWith("Digit", StringComparison.OrdinalIgnoreCase) && name.Length == 6 && char.IsAsciiDigit(name[5])) return name[5];
        if (name.StartsWith("Key", StringComparison.OrdinalIgnoreCase) && name.Length == 4 && char.IsAsciiLetter(name[3])) return char.ToUpperInvariant(name[3]);
        return Named.TryGetValue(name, out var code) ? code : null;
    }
}
