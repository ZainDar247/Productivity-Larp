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

    private ConfigStore(string dir, string backupDir, Config current, string? error, bool firstRun)
    {
        Dir = dir; BackupDir = backupDir; _current = current; _loadError = error; FirstRun = firstRun;
    }

    /// <summary>Where timestamped copies of config.json are kept (outside the config folder by default, so removing the app or its data never takes them along).</summary>
    public string BackupDir { get; }

    public Config Current { get { lock (_gate) return _current; } }
    public string? LoadError { get { lock (_gate) return _loadError; } }

    public static ConfigStore Open(string dir, string? backupDir = null)
    {
        Directory.CreateDirectory(dir);
        backupDir ??= Path.Combine(dir, "backups");
        var path = Path.Combine(dir, "config.json");

        // config.json vanished (folder wiped, app reinstalled...): bring back the newest backup instead of starting over
        if (!File.Exists(path) && LatestBackup(backupDir) is { } latest)
        {
            try { File.Copy(latest, path); } catch (IOException) { /* fall through to the starter menu */ }
        }

        var firstRun = !File.Exists(path);
        if (firstRun) File.WriteAllText(path, DefaultConfig.Json);

        try
        {
            var text = File.ReadAllText(path);
            var config = ConfigParser.Parse(text);
            if (!firstRun) Backup(backupDir, text);
            return new ConfigStore(dir, backupDir, config, null, firstRun);
        }
        catch (Exception e) when (e is LarpException or IOException or UnauthorizedAccessException)
        {
            return new ConfigStore(dir, backupDir, ConfigParser.Parse(DefaultConfig.Json), e.Message, firstRun);
        }
    }

    private const int KeepBackups = 30;

    private static string? LatestBackup(string backupDir) =>
        Directory.Exists(backupDir)
            ? Directory.EnumerateFiles(backupDir, "config-*.json").OrderByDescending(Path.GetFileName).FirstOrDefault()
            : null;

    /// <summary>Keep a timestamped copy of a valid config (skipped if identical to the newest copy). Never throws.</summary>
    private static void Backup(string backupDir, string json)
    {
        try
        {
            Directory.CreateDirectory(backupDir);
            if (LatestBackup(backupDir) is { } last && File.ReadAllText(last) == json) return;
            File.WriteAllText(Path.Combine(backupDir, $"config-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json"), json);
            foreach (var old in Directory.EnumerateFiles(backupDir, "config-*.json").OrderByDescending(Path.GetFileName).Skip(KeepBackups))
                File.Delete(old);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* a backup is a bonus, never a failure */ }
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
        Backup(BackupDir, json);
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
