namespace Larp.Core;

public enum TriggerMode { Press, Hold }

public enum MouseButton { Left, Right, Middle, Back, Forward }

/// <summary>How the menu is opened: a keyboard shortcut or a mouse button.</summary>
public abstract record Trigger(TriggerMode Mode, int HoldMs);

public sealed record KeyboardTrigger(string Shortcut, TriggerMode Mode, int HoldMs) : Trigger(Mode, HoldMs);

public sealed record MouseTrigger(MouseButton Button, TriggerMode Mode, int HoldMs) : Trigger(Mode, HoldMs);

public sealed record Behavior(int DwellMs, bool WarpCursor)
{
    public static Behavior Default { get; } = new(150, true);
}

/// <summary>What a leaf slice does.</summary>
public abstract record MenuAction;

/// <summary>Open a file, folder or app with its default handler.</summary>
public sealed record OpenAction(string Path) : MenuAction;

/// <summary>Open a remote-desktop file with mstsc.exe.</summary>
public sealed record RdpAction(string File, IReadOnlyList<string> Args) : MenuAction;

/// <summary>Start any program.</summary>
public sealed record RunAction(string Command, IReadOnlyList<string> Args, string? Cwd, bool Hidden) : MenuAction;

/// <summary>Open a URL in the default browser.</summary>
public sealed record UrlAction(string Url) : MenuAction;

/// <summary>Open the settings window.</summary>
public sealed record SettingsAction : MenuAction;

/// <summary>A node with <see cref="Children"/> is a folder (opens the next ring); otherwise it runs <see cref="Action"/>.</summary>
public sealed record Node(
    string Id,
    string? Label,
    string? Icon,
    string? Image,
    string? Display,
    string? Color,
    IReadOnlyList<Node>? Children,
    MenuAction? Action);

/// <summary>
/// The parts of the config the native side needs. Theme and per-slice appearance are
/// the UI's business: the host never re-serialises the file, it stores the UI's JSON as-is.
/// </summary>
public sealed record Config(int Version, Trigger Trigger, Behavior Behavior, IReadOnlyList<Node> Menu)
{
    public Node? FindNode(string id) => Find(Menu, id);

    private static Node? Find(IReadOnlyList<Node> nodes, string id)
    {
        foreach (var n in nodes)
        {
            if (n.Id == id) return n;
            if (n.Children is { } c && Find(c, id) is { } hit) return hit;
        }
        return null;
    }
}

public class LarpException(string message) : Exception(message);

public sealed class ConfigException(string message) : LarpException($"invalid config: {message}");

public sealed class MissingEnvException(string name)
    : LarpException($"environment variable `{name}` is not set (define it in .env)")
{
    public string Name { get; } = name;
}
