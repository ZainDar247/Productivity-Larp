using System.Diagnostics;

namespace Larp.Core;

/// <summary>An action with every ${ENV} placeholder resolved, ready to run.</summary>
public abstract record Prepared;

public sealed record OpenPrepared(string Target) : Prepared;

public sealed record SpawnPrepared(string Program, IReadOnlyList<string> Args, string? Cwd, bool Hidden) : Prepared;

public sealed record SettingsPrepared : Prepared;

public static class ActionPreparer
{
    public static Prepared Prepare(MenuAction action, Func<string, string?> env)
    {
        string Ex(string s) => EnvExpander.Expand(s, env);
        return action switch
        {
            OpenAction a => new OpenPrepared(Ex(a.Path)),
            UrlAction a => new OpenPrepared(Ex(a.Url)),
            RdpAction a => new SpawnPrepared("mstsc.exe", [Ex(a.File), .. a.Args.Select(Ex)], null, false),
            RunAction a => new SpawnPrepared(Ex(a.Command), a.Args.Select(Ex).ToList(), a.Cwd is null ? null : Ex(a.Cwd), a.Hidden),
            SettingsAction => new SettingsPrepared(),
            _ => throw new LarpException($"unsupported action {action.GetType().Name}"),
        };
    }
}

public static class ActionRunner
{
    /// <summary>Perform the side effect. <see cref="SettingsPrepared"/> is the host's job, not ours.</summary>
    public static void Run(Prepared p)
    {
        switch (p)
        {
            case OpenPrepared o:
                if (string.IsNullOrWhiteSpace(o.Target)) throw new LarpException("nothing to open (empty path)");
                try { Process.Start(new ProcessStartInfo(o.Target) { UseShellExecute = true })?.Dispose(); }
                catch (Exception e) { throw new LarpException($"could not open `{o.Target}`: {e.Message}"); }
                break;

            case SpawnPrepared s:
                if (string.IsNullOrWhiteSpace(s.Program)) throw new LarpException("no program to run (empty command)");
                var psi = new ProcessStartInfo(s.Program)
                {
                    UseShellExecute = false,
                    CreateNoWindow = s.Hidden,
                    WorkingDirectory = s.Cwd ?? "",
                };
                foreach (var a in s.Args) psi.ArgumentList.Add(a);
                try { Process.Start(psi)?.Dispose(); }
                catch (Exception e) { throw new LarpException($"could not start `{s.Program}`: {e.Message}"); }
                break;

            case SettingsPrepared:
                break;
        }
    }
}
