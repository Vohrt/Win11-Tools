using CommunityToolkit.Mvvm.ComponentModel;

namespace Win11EasyConfig.App.ViewModels.Settings;

/// <summary>
/// Shared base for every setting row (toggle / choice / numeric). Holds the availability state
/// the enablement engine computes from interlocks, read-only flags and OS support.
/// </summary>
public abstract partial class SettingItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDisabledReason))]
    public partial bool IsAvailable { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDisabledReason))]
    public partial string? DisabledReason { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool ShowDisabledReason => !IsAvailable && !string.IsNullOrEmpty(DisabledReason);

    public abstract string Id { get; }

    public abstract string Title { get; }

    public virtual IReadOnlyList<string> RequiresOn => [];

    public abstract Task RefreshAsync(CancellationToken cancellationToken = default);
}
