using System.Reflection;

namespace Larp.Core;

/// <summary>The bundled starter menu (config.example.json, embedded).</summary>
public static class DefaultConfig
{
    public static string Json { get; } = Load();

    private static string Load()
    {
        using var s = typeof(DefaultConfig).Assembly.GetManifestResourceStream("config.example.json")
            ?? throw new InvalidOperationException("embedded config.example.json missing");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}

/// <summary>
/// Owns config.json and .env in the config directory. A broken file is never overwritten:
/// the app falls back to the defaults in memory and reports <see cref="LoadError"/> instead.
/// </summary>
public sealed class ConfigStore
{
    private readonly object _gate = new();
    private Config _current;
    private string? _loadError;

    public string Dir { get; }
    public string ConfigPath => Path.Combine(Dir, "config.json");
    public bool FirstRun { get; }

    private ConfigStore(string dir, Config current, string? error, bool firstRun)
    {
        Dir = dir; _current = current; _loadError = error; FirstRun = firstRun;
    }

    public Config Current { get { lock (_gate) return _current; } }
    public string? LoadError { get { lock (_gate) return _loadError; } }

    public static ConfigStore Open(string dir)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        var firstRun = !File.Exists(path);
        if (firstRun) File.WriteAllText(path, DefaultConfig.Json);

        try
        {
            return new ConfigStore(dir, ConfigParser.Parse(File.ReadAllText(path)), null, firstRun);
        }
        catch (Exception e) when (e is LarpException or IOException or UnauthorizedAccessException)
        {
            return new ConfigStore(dir, ConfigParser.Parse(DefaultConfig.Json), e.Message, firstRun);
        }
    }

    /// <summary>The file exactly as stored, so the settings UI can repair a broken one.</summary>
    public string RawText()
    {
        try { return File.ReadAllText(ConfigPath); }
        catch (IOException) { return DefaultConfig.Json; }
    }

    /// <summary>Validate then persist the UI's JSON verbatim. Throws (and writes nothing) if invalid.</summary>
    public Config Save(string json)
    {
        var parsed = ConfigParser.Parse(json);
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, ConfigPath, overwrite: true); // atomic replace: never leaves a half-written config
        lock (_gate) { _current = parsed; _loadError = null; }
        return parsed;
    }

    /// <summary>Variables from &lt;config dir&gt;/.env, then ./.env, then the real environment. Re-read on each call.</summary>
    public Func<string, string?> EnvLookup()
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in new[] { Path.Combine(Dir, ".env"), Path.Combine(Directory.GetCurrentDirectory(), ".env") })
        {
            if (!File.Exists(p)) continue;
            foreach (var (k, v) in DotEnv.Parse(File.ReadAllText(p))) vars.TryAdd(k, v);
        }
        return name => vars.TryGetValue(name, out var v) ? v : Environment.GetEnvironmentVariable(name);
    }
}
