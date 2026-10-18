using System.ServiceProcess;
using Win11EasyConfig.Core.Abstractions;

namespace Win11EasyConfig.Core.Services;

/// <summary>
/// Reads service state through <see cref="ServiceController"/> and changes it through
/// <c>sc.exe</c>, which works reliably when elevated.
/// </summary>
public sealed class WindowsServiceManager(ICommandRunner commandRunner) : IWindowsServiceManager
{
    private readonly ICommandRunner _commandRunner = commandRunner;

    public ServiceSnapshot GetService(string serviceName)
    {
        try
        {
            using var controller = ServiceController.GetServices()
                .FirstOrDefault(s => string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));

            if (controller is null)
            {
                return new ServiceSnapshot(false, ServiceStartupMode.Unknown, false);
            }

            var mode = controller.StartType switch
            {
                ServiceStartMode.Disabled => ServiceStartupMode.Disabled,
                ServiceStartMode.Manual => ServiceStartupMode.Manual,
                ServiceStartMode.Automatic => ServiceStartupMode.Automatic,
                ServiceStartMode.Boot => ServiceStartupMode.Boot,
                ServiceStartMode.System => ServiceStartupMode.System,
                _ => ServiceStartupMode.Unknown
            };

            return new ServiceSnapshot(true, mode, controller.Status == ServiceControllerStatus.Running);
        }
        catch
        {
            return new ServiceSnapshot(false, ServiceStartupMode.Unknown, false);
        }
    }

    public Task SetStartupAsync(string serviceName, ServiceStartupMode mode, CancellationToken cancellationToken = default)
    {
        var value = mode switch
        {
            ServiceStartupMode.Disabled => "disabled",
            ServiceStartupMode.Manual => "demand",
            ServiceStartupMode.Automatic => "auto",
            ServiceStartupMode.AutomaticDelayed => "delayed-auto",
            ServiceStartupMode.Boot => "boot",
            ServiceStartupMode.System => "system",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported startup mode.")
        };

        return _commandRunner.RunCmdAsync($"sc config \"{serviceName}\" start= {value}", cancellationToken);
    }

    public Task StartAsync(string serviceName, CancellationToken cancellationToken = default) =>
        _commandRunner.RunCmdAsync($"sc start \"{serviceName}\"", cancellationToken);

    public Task StopAsync(string serviceName, CancellationToken cancellationToken = default) =>
        _commandRunner.RunCmdAsync($"sc stop \"{serviceName}\"", cancellationToken);
}
