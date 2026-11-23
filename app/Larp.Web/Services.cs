using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;
using Larp.Core;

namespace Larp.Web;

public sealed record LarpWebOptions(
    ConfigStore Store,
    IDesktop Desktop,
    /// <summary>Secret required on every request except static assets.</summary>
    string Token,
    /// <summary>Folder containing <c>ui/app.js</c> and <c>ui/app.css</c> (the built React bundle).</summary>
    string WebRoot,
    /// <summary>0 = pick a free port.</summary>
    int Port = 0)
{
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
}

public sealed record BusMessage(string Event, string DataJson);

/// <summary>Fan-out of server-sent events (menu:open, menu:close, config:changed) to every connected page.</summary>
public sealed class EventBus
{
    private readonly ConcurrentDictionary<Guid, Channel<BusMessage>> _subs = new();

    public int SubscriberCount => _subs.Count;

    public void Publish(string name, object? payload = null)
    {
        var msg = new BusMessage(name, System.Text.Json.JsonSerializer.Serialize(payload ?? new { }, JsonOpts));
        foreach (var ch in _subs.Values) ch.Writer.TryWrite(msg);
    }

    public Subscription Subscribe()
    {
        var id = Guid.NewGuid();
        var ch = Channel.CreateBounded<BusMessage>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });
        _subs[id] = ch;
        return new Subscription(ch.Reader, () => _subs.TryRemove(id, out _));
    }

    internal static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new(System.Text.Json.JsonSerializerDefaults.Web);

    public sealed class Subscription(ChannelReader<BusMessage> reader, Action onDispose) : IDisposable
    {
        public ChannelReader<BusMessage> Reader { get; } = reader;
        public void Dispose() => onDispose();
    }
}

/// <summary>Holds the most recent desktop screenshots shown behind the ring (the overlay's backdrop).</summary>
public sealed class ShotStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, byte[]> _shots = new();
    private int _next;

    public int Put(byte[] jpeg)
    {
        lock (_gate)
        {
            var id = ++_next;
            _shots[id] = jpeg;
            _shots.Remove(id - 2); // keep only the latest couple
            return id;
        }
    }

    public byte[]? Get(int id)
    {
        lock (_gate) return _shots.GetValueOrDefault(id);
    }
}
