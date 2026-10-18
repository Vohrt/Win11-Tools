using Win11EasyConfig.Core.Models;

namespace Win11EasyConfig.Core.Tweaks;

/// <summary>
/// A single reversible system setting. Implementations describe *what* to read and write;
/// they never talk to the UI.
/// </summary>
public interface ITweak
{
    string Id { get; }

    string Title { get; }

    string? Description { get; }

    TweakCategory Category { get; }

    EffectScope Effect { get; }

    /// <summary>
    /// True when the tweak can only be observed, not changed (e.g.
    /// "应用启动预取（只读）").
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Ids of tweaks that must be <see cref="TweakState.On"/> before this one can be changed.
    /// e.g. the memory-agent panels are disabled while SysMain is stopped, and 快速启动 is
    /// disabled while hibernation is off.
    /// </summary>
    IReadOnlyList<string> RequiresOn { get; }

    /// <summary>
    /// False when the current OS build does not support the tweak at all (the control is then
    /// shown greyed out with <see cref="UnsupportedReason"/>).
    /// </summary>
    bool IsSupported { get; }

    /// <summary>Reason shown to the user when <see cref="IsSupported"/> is false.</summary>
    string? UnsupportedReason { get; }

    /// <summary>Reads the current state from the machine.</summary>
    Task<TweakState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies the requested state.</summary>
    Task<TweakApplyResult> ApplyAsync(bool enable, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of applying a tweak. Failures carry a message the UI can surface.
/// </summary>
public sealed record TweakApplyResult(bool Success, string? Message = null)
{
    public static TweakApplyResult Ok() => new(true);

    public static TweakApplyResult Fail(string message) => new(false, message);
}
