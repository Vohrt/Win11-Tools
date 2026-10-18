namespace Win11EasyConfig.Core.Abstractions;

public enum ServiceStartupMode
{
    Unknown,
    Disabled,
    Manual,
    Automatic,
    AutomaticDelayed,
    Boot,
    System
}

public sealed record ServiceSnapshot(bool Exists, ServiceStartupMode StartupMode, bool IsRunning);

/// <summary>Query and control Windows services.</summary>
public interface IWindowsServiceManager
{
    ServiceSnapshot GetService(string serviceName);

    Task SetStartupAsync(string serviceName, ServiceStartupMode mode, CancellationToken cancellationToken = default);

    Task StartAsync(string serviceName, CancellationToken cancellationToken = default);

    Task StopAsync(string serviceName, CancellationToken cancellationToken = default);
}
