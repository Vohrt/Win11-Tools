using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;

namespace Win11EasyConfig.Core.Tweaks;

/// <summary>
/// One registry write. <see cref="Value"/> of <c>null</c> means "delete the value",
/// which is how the catalog restores Windows defaults.
/// </summary>
public sealed record RegistryWrite(string KeyPath, string ValueName, object? Value, bool DeleteWholeKey = false)
{
    public static RegistryWrite Delete(string keyPath, string valueName) => new(keyPath, valueName, null);

    /// <summary>Removes an entire key and its children (used to undo CLSID overrides).</summary>
    public static RegistryWrite DeleteTree(string keyPath) => new(keyPath, string.Empty, null, DeleteWholeKey: true);

    public static RegistryWrite Int(string keyPath, string valueName, int value) => new(keyPath, valueName, value);

    public static RegistryWrite String(string keyPath, string valueName, string value) => new(keyPath, valueName, value);

    public static RegistryWrite Binary(string keyPath, string valueName, byte[] value) => new(keyPath, valueName, value);
}

/// <summary>
/// A tweak expressed purely as registry data: a probe that decides the current state and two
/// sets of writes for the on/off transitions.
/// </summary>
public sealed class RegistryTweak : TweakBase
{
    private readonly IRegistryService _registry;
    private readonly Func<IRegistryService, TweakState> _probe;
    private readonly IReadOnlyList<RegistryWrite> _onWrites;
    private readonly IReadOnlyList<RegistryWrite> _offWrites;

    public RegistryTweak(
        string id,
        string title,
        TweakCategory category,
        IRegistryService registry,
        Func<IRegistryService, TweakState> probe,
        IReadOnlyList<RegistryWrite> onWrites,
        IReadOnlyList<RegistryWrite> offWrites)
        : base(id, title, category)
    {
        _registry = registry;
        _probe = probe;
        _onWrites = onWrites;
        _offWrites = offWrites;
    }

    public override Task<TweakState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(_probe(_registry));
        }
        catch
        {
            return Task.FromResult(TweakState.Unknown);
        }
    }

    public override Task<TweakApplyResult> ApplyAsync(bool enable, CancellationToken cancellationToken = default)
    {
        var writes = enable ? _onWrites : _offWrites;

        try
        {
            foreach (var write in writes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Write(write);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakApplyResult.Fail(ex.Message));
        }

        return Task.FromResult(TweakApplyResult.Ok());
    }

    private void Write(RegistryWrite write)
    {
        if (write.DeleteWholeKey)
        {
            _registry.DeleteKeyTree(write.KeyPath);
            return;
        }

        switch (write.Value)
        {
            case null:
                _registry.DeleteValue(write.KeyPath, write.ValueName);
                break;
            case int intValue:
                _registry.SetInt(write.KeyPath, write.ValueName, intValue);
                break;
            case string stringValue:
                _registry.SetString(write.KeyPath, write.ValueName, stringValue);
                break;
            case byte[] binaryValue:
                _registry.SetBinary(write.KeyPath, write.ValueName, binaryValue);
                break;
            default:
                throw new NotSupportedException($"Unsupported registry value type {write.Value.GetType()}.");
        }
    }
}
