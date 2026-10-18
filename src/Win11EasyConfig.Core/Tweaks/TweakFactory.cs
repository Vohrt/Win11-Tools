using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;

namespace Win11EasyConfig.Core.Tweaks;

/// <summary>
/// Helpers that turn the most common registry patterns into one-liners in the catalog.
/// </summary>
public sealed class TweakFactory(IRegistryService registry)
{
    private readonly IRegistryService _registry = registry;

    /// <summary>
    /// A value that exists with <paramref name="onValue"/> when on and <paramref name="offValue"/> when off.
    /// </summary>
    public RegistryTweak Value(
        string id,
        string title,
        TweakCategory category,
        string keyPath,
        string valueName,
        int onValue,
        int offValue,
        int? defaultWhenMissing = null,
        string? description = null,
        EffectScope effect = EffectScope.Immediate,
        IReadOnlyList<string>? requiresOn = null,
        bool isReadOnly = false,
        bool isSupported = true,
        string? unsupportedReason = null)
    {
        return new RegistryTweak(
            id,
            title,
            category,
            _registry,
            reg =>
            {
                var current = reg.GetInt(keyPath, valueName) ?? defaultWhenMissing;
                if (current is null)
                {
                    return TweakState.Unknown;
                }

                return current == onValue ? TweakState.On : TweakState.Off;
            },
            [RegistryWrite.Int(keyPath, valueName, onValue)],
            [RegistryWrite.Int(keyPath, valueName, offValue)])
        {
            Description = description,
            Effect = effect,
            RequiresOn = requiresOn ?? [],
            IsReadOnly = isReadOnly,
            IsSupported = isSupported,
            UnsupportedReason = unsupportedReason
        };
    }

    /// <summary>
    /// A policy-style value: turning the tweak on removes the policy (Windows default), turning it
    /// off writes <paramref name="offValue"/>.
    /// </summary>
    public RegistryTweak Policy(
        string id,
        string title,
        TweakCategory category,
        string keyPath,
        string valueName,
        int offValue,
        string? description = null,
        EffectScope effect = EffectScope.Immediate,
        IReadOnlyList<string>? requiresOn = null,
        bool isReadOnly = false,
        bool isSupported = true,
        string? unsupportedReason = null)
    {
        return new RegistryTweak(
            id,
            title,
            category,
            _registry,
            reg => reg.GetInt(keyPath, valueName) == offValue ? TweakState.Off : TweakState.On,
            [RegistryWrite.Delete(keyPath, valueName)],
            [RegistryWrite.Int(keyPath, valueName, offValue)])
        {
            Description = description,
            Effect = effect,
            RequiresOn = requiresOn ?? [],
            IsReadOnly = isReadOnly,
            IsSupported = isSupported,
            UnsupportedReason = unsupportedReason
        };
    }

    /// <summary>Fully custom registry tweak.</summary>
    public RegistryTweak Custom(
        string id,
        string title,
        TweakCategory category,
        Func<IRegistryService, TweakState> probe,
        IReadOnlyList<RegistryWrite> onWrites,
        IReadOnlyList<RegistryWrite> offWrites,
        string? description = null,
        EffectScope effect = EffectScope.Immediate,
        IReadOnlyList<string>? requiresOn = null,
        bool isReadOnly = false,
        bool isSupported = true,
        string? unsupportedReason = null)
    {
        return new RegistryTweak(id, title, category, _registry, probe, onWrites, offWrites)
        {
            Description = description,
            Effect = effect,
            RequiresOn = requiresOn ?? [],
            IsReadOnly = isReadOnly,
            IsSupported = isSupported,
            UnsupportedReason = unsupportedReason
        };
    }
}
