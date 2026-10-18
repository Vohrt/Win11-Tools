using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;

namespace Win11EasyConfig.Core.Tweaks;

/// <summary>
/// Enables/disables a Windows service. "On" sets the service to its normal startup mode and
/// starts it; "Off" disables and stops it. A service that does not exist reports as Off.
/// </summary>
public sealed class ServiceTweak : TweakBase
{
    private readonly IWindowsServiceManager _services;
    private readonly string _serviceName;
    private readonly ServiceStartupMode _enabledMode;
    private readonly bool _probeRunningStatus;

    public ServiceTweak(
        string id,
        string title,
        TweakCategory category,
        IWindowsServiceManager services,
        string serviceName,
        ServiceStartupMode enabledMode = ServiceStartupMode.Automatic,
        bool probeRunningStatus = false)
        : base(id, title, category)
    {
        _services = services;
        _serviceName = serviceName;
        _enabledMode = enabledMode;
        _probeRunningStatus = probeRunningStatus;
    }

    public override Task<TweakState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _services.GetService(_serviceName);
        if (!snapshot.Exists)
        {
            return Task.FromResult(TweakState.Off);
        }

        // When probeRunningStatus is set, the state is "is it actually running" rather than
        // the startup type; otherwise the startup type decides.
        if (_probeRunningStatus)
        {
            return Task.FromResult(snapshot.IsRunning ? TweakState.On : TweakState.Off);
        }

        return Task.FromResult(snapshot.StartupMode == ServiceStartupMode.Disabled ? TweakState.Off : TweakState.On);
    }

    public override async Task<TweakApplyResult> ApplyAsync(bool enable, CancellationToken cancellationToken = default)
    {
        try
        {
            if (enable)
            {
                await _services.SetStartupAsync(_serviceName, _enabledMode, cancellationToken).ConfigureAwait(false);
                await _services.StartAsync(_serviceName, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _services.StopAsync(_serviceName, cancellationToken).ConfigureAwait(false);
                await _services.SetStartupAsync(_serviceName, ServiceStartupMode.Disabled, cancellationToken).ConfigureAwait(false);
            }

            return TweakApplyResult.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TweakApplyResult.Fail(ex.Message);
        }
    }
}
