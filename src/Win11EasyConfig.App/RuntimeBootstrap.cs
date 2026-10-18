using System;
using System.Runtime.CompilerServices;

namespace Win11EasyConfig.App;

internal static class RuntimeBootstrap
{
    /// <summary>
    /// Runs before <c>Main</c>. When the app is published as a single self-contained file, the
    /// Windows App SDK bundles its native runtime beside the exe and locates it through
    /// <c>MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY</c>; pointing that at the app base directory
    /// here (before any WinUI type loads) is what makes single-file publishing work. It is a no-op
    /// for framework-dependent and folder builds.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable(
            "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
    }
}
