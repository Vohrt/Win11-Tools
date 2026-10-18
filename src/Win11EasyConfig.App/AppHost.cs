using Microsoft.Extensions.DependencyInjection;
using Win11EasyConfig.App.ViewModels;
using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Catalog;
using Win11EasyConfig.Core.Features;
using Win11EasyConfig.Core.Services;

namespace Win11EasyConfig.App;

/// <summary>
/// Composition root. Keeping wiring here means views resolve their view models instead of
/// constructing services themselves.
/// </summary>
public static class AppHost
{
    private static IServiceProvider? _services;

    public static IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("AppHost has not been initialized.");

    public static void Initialize()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<IRegistryService, RegistryService>();
        services.AddSingleton<ICommandRunner, CommandRunner>();
        services.AddSingleton<IWindowsServiceManager, WindowsServiceManager>();
        services.AddSingleton<WindowsInfoService>();
        services.AddSingleton<ShellService>();
        services.AddSingleton<TweakCatalog>();

        // Feature services
        services.AddSingleton<EdgeService>();
        services.AddSingleton<AppsService>();
        services.AddSingleton<HardwareInfoService>();

        // View models
        services.AddTransient<CategoryPageViewModel>();
        services.AddTransient<SystemInfoViewModel>();
        services.AddTransient<EdgeViewModel>();
        services.AddTransient<AppsViewModel>();

        _services = services.BuildServiceProvider();
    }

    public static T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();
}
