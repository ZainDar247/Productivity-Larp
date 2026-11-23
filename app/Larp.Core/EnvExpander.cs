using System.Text;

namespace Larp.Core;

public static class EnvExpander
{
    /// <summary>
    /// Replace <c>${NAME}</c> using <paramref name="lookup"/>. <c>$$</c> is a literal dollar sign.
    /// A missing variable throws, so a typo can never silently launch the wrong thing.
    /// </summary>
    public static string Expand(string input, Func<string, string?> lookup)
    {
        var sb = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c != '$' || i + 1 >= input.Length) { sb.Append(c); continue; }

            var next = input[i + 1];
            if (next == '$') { sb.Append('$'); i++; }
            else if (next == '{')
            {
                var end = input.IndexOf('}', i + 2);
                if (end < 0) throw new ConfigException($"unterminated `${{` in `{input}`");
                var name = input[(i + 2)..end].Trim();
                sb.Append(lookup(name) ?? throw new MissingEnvException(name));
                i = end;
            }
            else sb.Append('$');
        }
        return sb.ToString();
    }
}

public static class DotEnv
{
    /// <summary>Parse a dotenv body: <c>KEY=value</c>, <c>#</c> comments, optional <c>export</c> and quotes.</summary>
    public static IEnumerable<(string Key, string Value)> Parse(string body)
    {
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..];
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            if (val.Length >= 2 && ((val[0] == '"' && val[^1] == '"') || (val[0] == '\'' && val[^1] == '\'')))
                val = val[1..^1];
            yield return (key, val);
        }
    }
}
