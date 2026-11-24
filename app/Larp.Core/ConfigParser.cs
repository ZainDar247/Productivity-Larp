using System.Text.Json;

namespace Larp.Core;

/// <summary>Strict parser/validator for config.json. Unknown keys are ignored (forward compatible).</summary>
public static class ConfigParser
{
    public static Config Parse(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException e) { throw new ConfigException($"not valid JSON: {e.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new ConfigException("the top level must be an object");

            var version = OptInt(root, "version", "config") ?? 1;
            var triggers = ParseTriggers(Req(root, "trigger", "config"));
            var behavior = root.TryGetProperty("behavior", out var b) && b.ValueKind == JsonValueKind.Object
                ? new Behavior(
                    OptInt(b, "dwellMs", "behavior") ?? Behavior.Default.DwellMs,
                    OptBool(b, "warpCursor", "behavior") ?? Behavior.Default.WarpCursor)
                : Behavior.Default;

            var menuEl = Req(root, "menu", "config");
            if (menuEl.ValueKind != JsonValueKind.Array) throw new ConfigException("menu must be an array");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var menu = ParseNodes(menuEl, "menu", seen);
            return new Config(version, triggers, behavior, menu);
        }
    }

    /// <summary>`trigger` is one trigger object, or an array of them (e.g. a keyboard shortcut AND a mouse button).</summary>
    private static List<Trigger> ParseTriggers(JsonElement t)
    {
        if (t.ValueKind != JsonValueKind.Array) return [ParseTrigger(t, "trigger")];
        if (t.GetArrayLength() == 0) throw new ConfigException("trigger must have at least one entry");
        var list = new List<Trigger>();
        var i = 0;
        foreach (var e in t.EnumerateArray()) list.Add(ParseTrigger(e, $"trigger[{i++}]"));
        return list;
    }

    private static Trigger ParseTrigger(JsonElement t, string path)
    {
        var type = ReqStr(t, "type", path);
        var mode = OptStr(t, "mode", path) switch
        {
            null or "press" => TriggerMode.Press,
            "hold" => TriggerMode.Hold,
            var other => throw new ConfigException($"{path}.mode `{other}` must be press or hold"),
        };
        var hold = OptInt(t, "holdMs", path) ?? 350;
        if (hold < 0) throw new ConfigException(path + ".holdMs must not be negative");

        switch (type)
        {
            case "keyboard":
                var shortcut = ReqStr(t, "shortcut", path);
                if (string.IsNullOrWhiteSpace(shortcut)) throw new ConfigException(path + ".shortcut is empty");
                return new KeyboardTrigger(shortcut, mode, hold);
            case "mouse":
                var button = ReqStr(t, "button", path).ToLowerInvariant() switch
                {
                    "left" => MouseButton.Left,
                    "right" => MouseButton.Right,
                    "middle" => MouseButton.Middle,
                    "back" => MouseButton.Back,
                    "forward" => MouseButton.Forward,
                    var other => throw new ConfigException($"{path}.button `{other}` is not left/right/middle/back/forward"),
                };
                return new MouseTrigger(button, mode, hold);
            default:
                throw new ConfigException($"{path}.type `{type}` must be keyboard or mouse");
        }
    }

    private static List<Node> ParseNodes(JsonElement arr, string path, HashSet<string> seen)
    {
        var list = new List<Node>();
        var i = 0;
        foreach (var n in arr.EnumerateArray())
        {
            var here = $"{path}[{i++}]";
            if (n.ValueKind != JsonValueKind.Object) throw new ConfigException($"{here} must be an object");
            var id = ReqStr(n, "id", here);
            if (string.IsNullOrWhiteSpace(id)) throw new ConfigException($"{here} has an empty id");
            if (!seen.Add(id)) throw new ConfigException($"duplicate node id `{id}`");

            IReadOnlyList<Node>? children = null;
            if (n.TryGetProperty("children", out var c) && c.ValueKind != JsonValueKind.Null)
            {
                if (c.ValueKind != JsonValueKind.Array) throw new ConfigException($"{here}.children must be an array");
                children = ParseNodes(c, $"{here}.children", seen);
            }

            MenuAction? action = null;
            if (n.TryGetProperty("action", out var a) && a.ValueKind != JsonValueKind.Null)
                action = ParseAction(a, $"{here}.action");

            list.Add(new Node(id, OptStr(n, "label", here), OptStr(n, "icon", here), OptStr(n, "image", here),
                OptStr(n, "display", here), OptStr(n, "color", here), children, action));
        }
        return list;
    }

    private static MenuAction ParseAction(JsonElement a, string path)
    {
        if (a.ValueKind != JsonValueKind.Object) throw new ConfigException($"{path} must be an object");
        var type = ReqStr(a, "type", path);
        return type switch
        {
            "open" => new OpenAction(ReqStr(a, "path", path)),
            "rdp" => new RdpAction(ReqStr(a, "file", path), StrList(a, "args", path)),
            "run" => new RunAction(ReqStr(a, "command", path), StrList(a, "args", path), OptStr(a, "cwd", path), OptBool(a, "hidden", path) ?? false,
                OptBool(a, "terminal", path) ?? false, ParseShell(OptStr(a, "shell", path), path), OptBool(a, "keepOpen", path) ?? true),
            "url" => new UrlAction(ReqStr(a, "url", path)),
            "settings" => new SettingsAction(),
            _ => throw new ConfigException($"{path}.type `{type}` is not one of open, rdp, run, url, settings"),
        };
    }

    private static string ParseShell(string? shell, string path) => shell switch
    {
        null => "powershell",
        "powershell" or "pwsh" or "cmd" => shell,
        _ => throw new ConfigException($"{path}.shell `{shell}` is not one of powershell, pwsh, cmd"),
    };

    // ---- small typed accessors that name the offending path in errors ----
    private static JsonElement Req(JsonElement o, string name, string path) =>
        o.TryGetProperty(name, out var v) ? v : throw new ConfigException($"{path}.{name} is missing");

    private static string ReqStr(JsonElement o, string name, string path)
    {
        var v = Req(o, name, path);
        return v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new ConfigException($"{path}.{name} must be a string");
    }

    private static string? OptStr(JsonElement o, string name, string path)
    {
        if (!o.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new ConfigException($"{path}.{name} must be a string");
    }

    private static int? OptInt(JsonElement o, string name, string path)
    {
        if (!o.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : throw new ConfigException($"{path}.{name} must be a whole number");
    }

    private static bool? OptBool(JsonElement o, string name, string path)
    {
        if (!o.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw new ConfigException($"{path}.{name} must be true or false");
    }

    private static IReadOnlyList<string> StrList(JsonElement o, string name, string path)
    {
        if (!o.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return [];
        if (v.ValueKind != JsonValueKind.Array) throw new ConfigException($"{path}.{name} must be an array of strings");
        return v.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : throw new ConfigException($"{path}.{name} must be an array of strings"))
            .ToList();
    }
}
