namespace Larp.Core;

public enum TriggerOutcome
{
    None,
    /// <summary>Press mode: open the menu, or close it if it is already open.</summary>
    Toggle,
    /// <summary>Hold mode: the host should call <see cref="TriggerMachine.HoldElapsed"/> after the hold time.</summary>
    StartHoldTimer,
    /// <summary>Hold mode, hold time reached: open the menu.</summary>
    Open,
}

/// <summary>
/// Pure press/hold state machine for the trigger (no clocks, no Windows). The host feeds it
/// physical down/up events and a timer callback; it answers what to do. Auto-repeat is ignored.
/// </summary>
public sealed class TriggerMachine(TriggerMode mode)
{
    private bool _down;
    private int _generation;

    public int Generation => _generation;

    public TriggerOutcome Down()
    {
        if (_down) return TriggerOutcome.None; // key auto-repeat
        _down = true;
        _generation++;
        return mode == TriggerMode.Press ? TriggerOutcome.Toggle : TriggerOutcome.StartHoldTimer;
    }

    public TriggerOutcome Up()
    {
        _down = false;
        return TriggerOutcome.None;
    }

    /// <summary>The hold timer started for <paramref name="generation"/> has fired.</summary>
    public TriggerOutcome HoldElapsed(int generation, bool menuVisible) =>
        _down && generation == _generation && !menuVisible ? TriggerOutcome.Open : TriggerOutcome.None;
}
