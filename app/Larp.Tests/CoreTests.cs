using Larp.Core;
using Xunit;

namespace Larp.Tests;

public class EnvTests
{
    private static string? Look(string k) => k switch { "A" => "alpha", "DIR" => @"C:\rdp", _ => null };

    [Fact] public void Expands_variables() =>
        Assert.Equal(@"C:\rdp\admin\alpha.rdp", EnvExpander.Expand(@"${DIR}\admin\${A}.rdp", Look));

    [Fact] public void Plain_text_and_escaped_dollar() =>
        Assert.Equal("cost $5 and $x", EnvExpander.Expand("cost $$5 and $x", Look));

    [Fact] public void Missing_variable_throws_and_names_it() =>
        Assert.Equal("NOPE", Assert.Throws<MissingEnvException>(() => EnvExpander.Expand("${NOPE}", Look)).Name);

    [Fact] public void Unterminated_throws() =>
        Assert.Throws<ConfigException>(() => EnvExpander.Expand("${A", Look));

    [Fact] public void Dotenv_parsing()
    {
        var v = DotEnv.Parse("# c\nA=1\nexport B=\"two words\"\n\nC='x'\nbad\r\nD=4\r\n").ToList();
        Assert.Equal([("A", "1"), ("B", "two words"), ("C", "x"), ("D", "4")], v);
    }
}

public class ConfigParserTests
{
    [Fact] public void Bundled_example_is_valid()
    {
        var c = ConfigParser.Parse(DefaultConfig.Json);
        Assert.IsType<KeyboardTrigger>(c.Trigger);
        Assert.NotNull(c.FindNode("rdp-admin-1"));
        Assert.IsType<RdpAction>(c.FindNode("rdp-admin-1")!.Action);
        Assert.NotNull(c.FindNode("rdp")!.Children);
    }

    [Fact] public void Duplicate_ids_rejected() =>
        Assert.Contains("duplicate", Assert.Throws<ConfigException>(() => ConfigParser.Parse(
            """{"trigger":{"type":"keyboard","shortcut":"F1"},"menu":[{"id":"a"},{"id":"a"}]}""")).Message);

    [Fact] public void Duplicate_ids_rejected_across_levels() =>
        Assert.Throws<ConfigException>(() => ConfigParser.Parse(
            """{"trigger":{"type":"keyboard","shortcut":"F1"},"menu":[{"id":"a","children":[{"id":"b"}]},{"id":"b"}]}"""));

    [Fact] public void Mouse_trigger_and_defaults()
    {
        var c = ConfigParser.Parse("""{"trigger":{"type":"mouse","button":"middle","mode":"hold"},"menu":[]}""");
        Assert.Equal(new MouseTrigger(MouseButton.Middle, TriggerMode.Hold, 350), c.Trigger);
        Assert.Equal(Behavior.Default, c.Behavior);
    }

    [Theory]
    [InlineData("""{"trigger":{"type":"keyboard","shortcut":"F1"},"menu":[{"id":"a","action":{"type":"format-disk"}}]}""")]
    [InlineData("""{"trigger":{"type":"keyboard","shortcut":""},"menu":[]}""")]
    [InlineData("""{"trigger":{"type":"keyboard","shortcut":"F1","mode":"sometimes"},"menu":[]}""")]
    [InlineData("""{"trigger":{"type":"mouse","button":"thumb"},"menu":[]}""")]
    [InlineData("""{"trigger":{"type":"keyboard","shortcut":"F1"},"menu":[{"id":" "}]}""")]
    [InlineData("""{"trigger":{"type":"keyboard","shortcut":"F1"}}""")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    public void Bad_configs_rejected(string json) => Assert.IsAssignableFrom<LarpException>(Record.Exception(() => ConfigParser.Parse(json)));

    [Fact] public void Unknown_keys_are_tolerated_for_forward_compatibility() =>
        ConfigParser.Parse("""{"trigger":{"type":"keyboard","shortcut":"F1","future":1},"theme":{"x":1},"menu":[{"id":"a","future":true}]}""");
}

public class ActionTests
{
    private static string? Look(string k) => k == "RDP_DIR" ? @"C:\rdp" : null;

    [Fact] public void Rdp_uses_mstsc_with_expanded_path_and_args() =>
        Assert.Equal(new SpawnPrepared("mstsc.exe", [@"C:\rdp\admin.rdp", "/admin"], null, false),
            ActionPreparer.Prepare(new RdpAction(@"${RDP_DIR}\admin.rdp", ["/admin"]), Look),
            new PreparedComparer());

    [Fact] public void Run_expands_every_field()
    {
        var p = Assert.IsType<SpawnPrepared>(ActionPreparer.Prepare(new RunAction("x", ["${RDP_DIR}"], "${RDP_DIR}", true), Look));
        Assert.Equal([@"C:\rdp"], p.Args);
        Assert.Equal(@"C:\rdp", p.Cwd);
        Assert.True(p.Hidden);
    }

    [Fact] public void Terminal_run_is_prepared_with_expanded_command_shell_and_keep_open()
    {
        var p = Assert.IsType<TerminalPrepared>(ActionPreparer.Prepare(new RunAction("dir ${RDP_DIR}", [], "${RDP_DIR}", false, true, "cmd", false), Look));
        Assert.Equal(@"dir C:\rdp", p.CommandLine);
        Assert.Equal("cmd", p.Shell);
        Assert.False(p.KeepOpen);
        Assert.Equal(@"C:\rdp", p.Cwd);
    }

    [Fact] public void Terminal_run_defaults_to_powershell_and_keeps_the_window_open()
    {
        var p = Assert.IsType<TerminalPrepared>(ActionPreparer.Prepare(new RunAction("npm run dev", [], null, false, Terminal: true), Look));
        Assert.Equal("powershell", p.Shell);
        Assert.True(p.KeepOpen);
    }

    [Fact] public void Terminal_launch_uses_a_visible_console_with_the_right_flags()
    {
        var keep = ActionRunner.TerminalStart(new TerminalPrepared("npm run dev", "powershell", true, null));
        Assert.Equal("powershell.exe", keep.FileName);
        Assert.Equal(["-NoExit", "-Command", "npm run dev"], keep.ArgumentList);
        Assert.False(keep.CreateNoWindow);

        var close = ActionRunner.TerminalStart(new TerminalPrepared("npm run dev", "pwsh", false, @"C:\app"));
        Assert.Equal("pwsh.exe", close.FileName);
        Assert.Equal(["-Command", "npm run dev"], close.ArgumentList);
        Assert.Equal(@"C:\app", close.WorkingDirectory);

        var cmdKeep = ActionRunner.TerminalStart(new TerminalPrepared("echo \"hi\"", "cmd", true, null));
        Assert.Equal("cmd.exe", cmdKeep.FileName);
        Assert.Equal("/s /k \"echo \"hi\"\"", cmdKeep.Arguments);
        Assert.Equal("/s /c \"x\"", ActionRunner.TerminalStart(new TerminalPrepared("x", "cmd", false, null)).Arguments);
    }

    [Fact] public void Empty_terminal_command_rejected() =>
        Assert.Throws<LarpException>(() => ActionRunner.Run(new TerminalPrepared("  ", "powershell", true, null)));

    [Fact] public void Parser_reads_terminal_fields_and_rejects_unknown_shells()
    {
        var ok = ConfigParser.Parse("""{"trigger":{"type":"keyboard","shortcut":"F1"},"menu":[{"id":"t","action":{"type":"run","command":"npm run dev","terminal":true,"shell":"cmd","keepOpen":false}}]}""");
        var a = Assert.IsType<RunAction>(ok.Menu[0].Action);
        Assert.True(a.Terminal);
        Assert.Equal("cmd", a.Shell);
        Assert.False(a.KeepOpen);
        Assert.Throws<ConfigException>(() => ConfigParser.Parse("""{"trigger":{"type":"keyboard","shortcut":"F1"},"menu":[{"id":"t","action":{"type":"run","command":"x","terminal":true,"shell":"bash"}}]}"""));
    }

    [Fact] public void Missing_env_fails_before_anything_launches() =>
        Assert.Throws<MissingEnvException>(() => ActionPreparer.Prepare(new OpenAction("${SECRET_PATH}"), Look));

    [Fact] public void Settings_is_left_to_the_host() =>
        Assert.IsType<SettingsPrepared>(ActionPreparer.Prepare(new SettingsAction(), Look));

    [Fact] public void Starting_a_missing_program_is_reported() =>
        Assert.Throws<LarpException>(() => ActionRunner.Run(new SpawnPrepared("definitely-not-a-real-binary-xyz", [], null, false)));

    [Fact] public void Empty_open_target_rejected() =>
        Assert.Throws<LarpException>(() => ActionRunner.Run(new OpenPrepared("  ")));

    // records holding lists compare lists by reference; compare structurally here
    private sealed class PreparedComparer : IEqualityComparer<Prepared>
    {
        public bool Equals(Prepared? a, Prepared? b) => a is SpawnPrepared x && b is SpawnPrepared y
            && x.Program == y.Program && x.Args.SequenceEqual(y.Args) && x.Cwd == y.Cwd && x.Hidden == y.Hidden;
        public int GetHashCode(Prepared p) => 0;
    }
}

public class ShortcutTests
{
    [Fact] public void Parses_the_default() => Assert.Equal(new Shortcut(true, true, false, false, 'Z'), Shortcut.Parse("Ctrl+Shift+Z"));

    [Theory]
    [InlineData("alt+f5", false, false, true, false, 0x74)]
    [InlineData("Win+Space", false, false, false, true, 0x20)]
    [InlineData("Ctrl+Alt+ArrowUp", true, false, true, false, 0x26)]
    [InlineData("Super+KeyQ", false, false, false, true, 'Q')]
    [InlineData("Ctrl+Digit7", true, false, false, false, '7')]
    [InlineData("F12", false, false, false, false, 0x7B)]
    [InlineData("Ctrl+Backquote", true, false, false, false, 0xC0)]
    public void Parses_variants(string text, bool ctrl, bool shift, bool alt, bool win, int vk) =>
        Assert.Equal(new Shortcut(ctrl, shift, alt, win, vk), Shortcut.Parse(text));

    [Theory]
    [InlineData("Ctrl+Shift")]            // no key
    [InlineData("Ctrl+Shift+Nonsense")]   // unknown key
    [InlineData("Ctrl+A+B")]              // two keys
    [InlineData("")]
    public void Rejects_unusable(string text) => Assert.Throws<ConfigException>(() => Shortcut.Parse(text));
}

public class TriggerMachineTests
{
    [Fact] public void Press_mode_toggles_once_per_physical_press_and_ignores_auto_repeat()
    {
        var m = new TriggerMachine(TriggerMode.Press);
        Assert.Equal(TriggerOutcome.Toggle, m.Down());
        Assert.Equal(TriggerOutcome.None, m.Down());
        Assert.Equal(TriggerOutcome.None, m.Down());
        m.Up();
        Assert.Equal(TriggerOutcome.Toggle, m.Down());
    }

    [Fact] public void Hold_mode_opens_only_if_still_held_when_the_timer_fires()
    {
        var m = new TriggerMachine(TriggerMode.Hold);
        Assert.Equal(TriggerOutcome.StartHoldTimer, m.Down());
        Assert.Equal(TriggerOutcome.Open, m.HoldElapsed(m.Generation, menuVisible: false));
    }

    [Fact] public void Hold_mode_released_early_does_nothing()
    {
        var m = new TriggerMachine(TriggerMode.Hold);
        m.Down(); var gen = m.Generation;
        m.Up();
        Assert.Equal(TriggerOutcome.None, m.HoldElapsed(gen, false));
    }

    [Fact] public void A_stale_timer_from_an_earlier_press_cannot_open_a_later_one()
    {
        var m = new TriggerMachine(TriggerMode.Hold);
        m.Down(); var old = m.Generation; m.Up();
        m.Down();
        Assert.Equal(TriggerOutcome.None, m.HoldElapsed(old, false));
    }

    [Fact] public void Hold_does_not_reopen_a_visible_menu()
    {
        var m = new TriggerMachine(TriggerMode.Hold);
        m.Down();
        Assert.Equal(TriggerOutcome.None, m.HoldElapsed(m.Generation, menuVisible: true));
    }
}

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "larp-test-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
}

public class ConfigStoreTests
{
    [Fact] public void First_run_writes_the_starter_menu()
    {
        using var d = new TempDir();
        var s = ConfigStore.Open(Path.Combine(d.Path, "cfg"));
        Assert.True(s.FirstRun);
        Assert.Null(s.LoadError);
        Assert.True(File.Exists(s.ConfigPath));
        Assert.NotNull(s.Current.FindNode("rdp"));
    }

    [Fact] public void A_broken_file_is_never_overwritten_and_the_app_falls_back_to_defaults()
    {
        using var d = new TempDir();
        File.WriteAllText(Path.Combine(d.Path, "config.json"), "{ broken");
        var s = ConfigStore.Open(d.Path);
        Assert.False(s.FirstRun);
        Assert.NotNull(s.LoadError);
        Assert.NotNull(s.Current.FindNode("rdp")); // running on defaults
        Assert.Equal("{ broken", File.ReadAllText(s.ConfigPath)); // the user's file is untouched
        Assert.Equal("{ broken", s.RawText());
    }

    [Fact] public void Save_persists_the_UIs_json_verbatim_and_updates_current()
    {
        using var d = new TempDir();
        var s = ConfigStore.Open(d.Path);
        const string json = """{ "trigger": {"type":"keyboard","shortcut":"F9"}, "theme": {"accent":"#fff","futureKey":1}, "menu": [{"id":"x"}] }""";
        s.Save(json);
        Assert.Equal(json, File.ReadAllText(s.ConfigPath)); // unknown/theme keys survive: never re-serialised
        Assert.NotNull(s.Current.FindNode("x"));
        Assert.Null(s.LoadError);
        Assert.False(File.Exists(s.ConfigPath + ".tmp"));
    }

    [Fact] public void Invalid_save_writes_nothing()
    {
        using var d = new TempDir();
        var s = ConfigStore.Open(d.Path);
        var before = File.ReadAllText(s.ConfigPath);
        Assert.Throws<ConfigException>(() => s.Save("""{"trigger":{"type":"keyboard","shortcut":"F1"},"menu":[{"id":"a"},{"id":"a"}]}"""));
        Assert.Equal(before, File.ReadAllText(s.ConfigPath));
    }

    [Fact] public void Env_lookup_reads_dotenv_then_the_process_environment()
    {
        using var d = new TempDir();
        var s = ConfigStore.Open(d.Path);
        File.WriteAllText(Path.Combine(d.Path, ".env"), "LARP_TEST_FROM_FILE=file\nLARP_TEST_BOTH=file\n");
        Environment.SetEnvironmentVariable("LARP_TEST_BOTH", "process");
        Environment.SetEnvironmentVariable("LARP_TEST_ONLY_PROC", "proc");
        try
        {
            var look = s.EnvLookup();
            Assert.Equal("file", look("LARP_TEST_FROM_FILE"));
            Assert.Equal("file", look("LARP_TEST_BOTH"));       // .env wins over the process environment
            Assert.Equal("proc", look("LARP_TEST_ONLY_PROC"));
            Assert.Null(look("LARP_TEST_NOT_DEFINED"));
        }
        finally { Environment.SetEnvironmentVariable("LARP_TEST_BOTH", null); Environment.SetEnvironmentVariable("LARP_TEST_ONLY_PROC", null); }
    }
}

public class ImageTests
{
    [Fact] public void Rejects_unknown_extension() => Assert.Throws<LarpException>(() => ImageLoader.ToDataUrl("x.exe"));
    [Fact] public void Rejects_missing_file() => Assert.Throws<LarpException>(() => ImageLoader.ToDataUrl("/nope/missing.png"));

    [Fact] public void Encodes_a_file()
    {
        using var d = new TempDir();
        var f = Path.Combine(d.Path, "a.PNG");
        File.WriteAllBytes(f, [1, 2, 3]);
        Assert.Equal("data:image/png;base64,AQID", ImageLoader.ToDataUrl(f));
    }
}
