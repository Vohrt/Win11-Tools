namespace Win11EasyConfig.Core.Models;

/// <summary>
/// The resolved on/off state of a tweak, or <see cref="Unknown"/> when it could not be probed.
/// </summary>
public enum TweakState
{
    Unknown,
    On,
    Off
}
