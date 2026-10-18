using Win11EasyConfig.Core.Models;

namespace Win11EasyConfig.Core.Tweaks;

/// <summary>
/// Common metadata plumbing so concrete tweaks only implement probing and applying.
/// </summary>
public abstract class TweakBase : ITweak
{
    protected TweakBase(string id, string title, TweakCategory category)
    {
        Id = id;
        Title = title;
        Category = category;
    }

    public string Id { get; }

    public string Title { get; }

    public string? Description { get; init; }

    public TweakCategory Category { get; }

    public EffectScope Effect { get; init; } = EffectScope.Immediate;

    public bool IsReadOnly { get; init; }

    public IReadOnlyList<string> RequiresOn { get; init; } = [];

    public bool IsSupported { get; init; } = true;

    public string? UnsupportedReason { get; init; }

    public abstract Task<TweakState> GetStateAsync(CancellationToken cancellationToken = default);

    public abstract Task<TweakApplyResult> ApplyAsync(bool enable, CancellationToken cancellationToken = default);
}
