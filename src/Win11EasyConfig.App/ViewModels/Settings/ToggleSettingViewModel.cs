using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.App.ViewModels.Settings;

/// <summary>
/// Presents one <see cref="ITweak"/> as a toggle row. Applying is guarded so that programmatic
/// state refreshes never trigger a write, and it reports state changes so dependent rows can
/// recompute their availability (the SysMain / hibernation interlocks).
/// </summary>
public sealed partial class ToggleSettingViewModel : SettingItemViewModel
{
    private readonly ITweak _tweak;
    private bool _suppressApply;

    /// <summary>Raised after a refresh or apply changes the observed on/off state.</summary>
    public event Action? StateChanged;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial bool IsUnknown { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public ToggleSettingViewModel(ITweak tweak)
    {
        _tweak = tweak;
    }

    public override string Id => _tweak.Id;

    public override string Title => _tweak.Title;

    public override IReadOnlyList<string> RequiresOn => _tweak.RequiresOn;

    public string? Description => _tweak.Description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(_tweak.Description);

    public bool IsReadOnly => _tweak.IsReadOnly;

    public bool IsSupported => _tweak.IsSupported;

    public string? UnsupportedReason => _tweak.UnsupportedReason;

    public TweakState CurrentState { get; private set; } = TweakState.Unknown;

    public string EffectLabel => _tweak.Effect switch
    {
        EffectScope.RestartExplorer => "需重启资源管理器",
        EffectScope.SignOut => "需注销后生效",
        EffectScope.Reboot => "需重启电脑",
        _ => "立即生效"
    };

    public bool ShowEffectBadge => _tweak.Effect != EffectScope.Immediate;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public override async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var state = await _tweak.GetStateAsync(cancellationToken).ConfigureAwait(true);
            SetStateWithoutApplying(state);
        }
        catch
        {
            SetStateWithoutApplying(TweakState.Unknown);
        }
        finally
        {
            IsBusy = false;
        }

        StateChanged?.Invoke();
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_suppressApply)
        {
            return;
        }

        _ = ApplyAsync(value);
    }

    private void SetStateWithoutApplying(TweakState state)
    {
        _suppressApply = true;
        CurrentState = state;
        IsUnknown = state == TweakState.Unknown;
        IsOn = state == TweakState.On;
        _suppressApply = false;
    }

    private async Task ApplyAsync(bool enable)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _tweak.ApplyAsync(enable).ConfigureAwait(true);
            if (!result.Success)
            {
                ErrorMessage = result.Message;
            }

            // Re-read so the UI always reflects reality, even when the write was silently blocked.
            var state = await _tweak.GetStateAsync().ConfigureAwait(true);
            SetStateWithoutApplying(state);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        StateChanged?.Invoke();
    }

    [RelayCommand]
    private Task Reload() => RefreshAsync();
}
