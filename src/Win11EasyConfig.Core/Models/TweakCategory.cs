namespace Win11EasyConfig.Core.Models;

/// <summary>
/// Logical grouping used to build the navigation sections of the app.
/// </summary>
public enum TweakCategory
{
    System,
    Advanced,
    Explorer,
    Taskbar,
    Privacy,
    Performance,
    Network
}

/// <summary>
/// What the user must do before a change takes full effect.
/// </summary>
public enum EffectScope
{
    Immediate,
    RestartExplorer,
    SignOut,
    Reboot
}
