using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Catalog;
using Win11EasyConfig.Core.Services;

namespace Win11EasyConfig.Core.Tests;

/// <summary>
/// In-memory registry that records every write/delete so apply results can be asserted.
/// Reads return the current (fake) state, mirroring the real registry.
/// </summary>
internal sealed class FakeRegistryService : IRegistryService
{
    private readonly Dictionary<(string Key, string Name), object> _values = new();

    public List<(string KeyPath, string ValueName, object? Value)> Writes { get; } = [];

    public List<(string KeyPath, string ValueName)> Deletes { get; } = [];

    public List<string> DeletedTrees { get; } = [];

    /// <summary>Seeds a value without recording it as a write (test setup).</summary>
    public void SetRaw(string keyPath, string valueName, object value) =>
        _values[(keyPath, valueName)] = value;

    public int? GetInt(string keyPath, string valueName) =>
        _values.TryGetValue((keyPath, valueName), out var value) && value is int i ? i : null;

    public string? GetString(string keyPath, string valueName) =>
        _values.TryGetValue((keyPath, valueName), out var value) ? value as string : null;

    public byte[]? GetBinary(string keyPath, string valueName) =>
        _values.TryGetValue((keyPath, valueName), out var value) && value is byte[] bytes
            ? (byte[])bytes.Clone()
            : null;

    public void SetInt(string keyPath, string valueName, int value)
    {
        Writes.Add((keyPath, valueName, value));
        _values[(keyPath, valueName)] = value;
    }

    public void SetString(string keyPath, string valueName, string value)
    {
        Writes.Add((keyPath, valueName, value));
        _values[(keyPath, valueName)] = value;
    }

    public void SetBinary(string keyPath, string valueName, byte[] value)
    {
        Writes.Add((keyPath, valueName, (byte[])value.Clone()));
        _values[(keyPath, valueName)] = (byte[])value.Clone();
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        Deletes.Add((keyPath, valueName));
        _values.Remove((keyPath, valueName));
    }

    public void DeleteKeyTree(string keyPath)
    {
        DeletedTrees.Add(keyPath);
        foreach (var key in _values.Keys
                     .Where(k => k.Key.StartsWith(keyPath, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            _values.Remove(key);
        }
    }

    public IReadOnlyList<string> GetSubKeyNames(string keyPath) => [];
}

/// <summary>Command runner that never executes anything; launches are recorded.</summary>
internal sealed class FakeCommandRunner : ICommandRunner
{
    public List<string> Launched { get; } = [];

    public Task<CommandResult> RunCmdAsync(string command, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommandResult(0, string.Empty, string.Empty));

    public Task<CommandResult> RunPowerShellAsync(string command, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommandResult(0, string.Empty, string.Empty));

    public void Launch(string fileName, string? arguments = null) =>
        Launched.Add(string.IsNullOrEmpty(arguments) ? fileName : $"{fileName} {arguments}");
}

/// <summary>Service manager that reports every service as missing and ignores control calls.</summary>
internal sealed class FakeWindowsServiceManager : IWindowsServiceManager
{
    public ServiceSnapshot GetService(string serviceName) =>
        new(Exists: false, StartupMode: ServiceStartupMode.Unknown, IsRunning: false);

    public Task SetStartupAsync(string serviceName, ServiceStartupMode mode, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StartAsync(string serviceName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StopAsync(string serviceName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal static class TestCatalog
{
    /// <summary>
    /// Builds the real catalog on top of fakes. <see cref="WindowsInfoService"/> and
    /// <see cref="ShellService"/> are stateless readers, so the real ones are fine here.
    /// </summary>
    public static TweakCatalog Create(FakeRegistryService? registry = null) =>
        new(registry ?? new FakeRegistryService(),
            new FakeCommandRunner(),
            new FakeWindowsServiceManager(),
            new WindowsInfoService(),
            new ShellService());
}
