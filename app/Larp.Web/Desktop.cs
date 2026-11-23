using Larp.Core;

namespace Larp.Web;

/// <summary>
/// Everything the web layer needs from the machine it runs on. The Windows shell implements it
/// for real; the cross-platform dev host fakes it, which is what lets the MVC backend and the
/// React UI be exercised (and tested) without Windows.
/// </summary>
public interface IDesktop
{
    /// <summary>Register the trigger described by <paramref name="config"/>. Throws <see cref="LarpException"/> if it can't be registered.</summary>
    Task ApplyTriggerAsync(Config config);

    /// <summary>Execute a resolved action (anything except Settings, which goes through <see cref="OpenSettingsAsync"/>).</summary>
    Task RunAsync(Prepared action);

    Task OpenSettingsAsync();
    Task CloseOverlayAsync();

    /// <summary>The overlay page has drawn its first frame: reveal the window.</summary>
    Task OverlayReadyAsync();

    /// <summary>Move the pointer; coordinates are CSS px inside the overlay.</summary>
    Task WarpCursorAsync(double x, double y);

    Task<string?> PickFileAsync(string? title, IReadOnlyList<string>? extensions);
    Task OpenConfigDirAsync();
}
