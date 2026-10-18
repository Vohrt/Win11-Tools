using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11EasyConfig.App.ViewModels.Settings;
using Win11EasyConfig.Core.Catalog;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.App.ViewModels;

/// <summary>
/// Backs a category page. Beyond listing settings, it runs the enablement engine that applies
/// interlocks: a row that depends on another (via <c>RequiresOn</c>) is greyed out
/// until the parent toggle is on, read-only rows can be seen but not changed, and unsupported rows
/// are disabled with an explanation.
/// </summary>
public sealed partial class CategoryPageViewModel : ObservableObject
{
    private readonly TweakCatalog _catalog;
    private readonly Dictionary<string, ToggleSettingViewModel> _togglesById =
        new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    public partial string Header { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Subtitle { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public CategoryPageViewModel(TweakCatalog catalog)
    {
        _catalog = catalog;
    }

    public ObservableCollection<ToggleSettingViewModel> Toggles { get; } = [];

    public ObservableCollection<ChoiceSettingViewModel> Choices { get; } = [];

    public ObservableCollection<NumericSettingViewModel> Numerics { get; } = [];

    public ObservableCollection<ActionGroupViewModel> ActionGroups { get; } = [];

    public bool HasToggles => Toggles.Count > 0;

    public bool HasChoices => Choices.Count > 0;

    public bool HasNumerics => Numerics.Count > 0;

    public bool HasActionGroups => ActionGroups.Count > 0;

    public async Task InitializeAsync(TweakCategory category, string header, string? subtitle = null)
    {
        Header = header;
        Subtitle = subtitle;

        Toggles.Clear();
        Choices.Clear();
        Numerics.Clear();
        ActionGroups.Clear();
        _togglesById.Clear();

        foreach (var tweak in _catalog.TweaksIn(category))
        {
            var vm = new ToggleSettingViewModel(tweak);
            vm.StateChanged += RecomputeEnablement;
            Toggles.Add(vm);
            _togglesById[vm.Id] = vm;
        }

        foreach (var choice in _catalog.ChoicesIn(category))
        {
            Choices.Add(new ChoiceSettingViewModel(choice));
        }

        foreach (var numeric in _catalog.NumericsIn(category))
        {
            Numerics.Add(new NumericSettingViewModel(numeric));
        }

        foreach (var group in _catalog.ActionsIn(category).GroupBy(a => a.GroupName))
        {
            ActionGroups.Add(new ActionGroupViewModel(
                group.Key,
                group.Select(a => new ActionCommandViewModel(a))));
        }

        OnPropertyChanged(nameof(HasToggles));
        OnPropertyChanged(nameof(HasChoices));
        OnPropertyChanged(nameof(HasNumerics));
        OnPropertyChanged(nameof(HasActionGroups));

        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            // Probing can shell out (DISM/netsh/PowerShell), so run the reads concurrently.
            await Task.WhenAll(
                Toggles.Select(t => t.RefreshAsync())
                    .Concat(Choices.Select(c => c.RefreshAsync()))
                    .Concat(Numerics.Select(n => n.RefreshAsync())))
                .ConfigureAwait(true);

            RecomputeEnablement();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Re-evaluates every dependent row against the current toggle states. Called after each
    /// refresh and whenever a toggle flips, so turning SysMain off immediately greys the
    /// memory-management rows (and turning it back on re-enables them).
    /// </summary>
    private void RecomputeEnablement()
    {
        foreach (var toggle in Toggles)
        {
            if (!toggle.IsSupported)
            {
                Disable(toggle, toggle.UnsupportedReason ?? "当前 Windows 版本不支持此项。");
            }
            else if (toggle.IsReadOnly)
            {
                Disable(toggle, "此项为只读，仅反映系统当前状态。");
            }
            else
            {
                ApplyDependency(toggle);
            }
        }

        foreach (var choice in Choices)
        {
            ApplyDependency(choice);
        }

        foreach (var numeric in Numerics)
        {
            ApplyDependency(numeric);
        }
    }

    private void ApplyDependency(SettingItemViewModel item)
    {
        foreach (var id in item.RequiresOn)
        {
            // A parent outside this page can't gate the row, so only known toggles count.
            if (_togglesById.TryGetValue(id, out var parent) && parent.CurrentState != TweakState.On)
            {
                Disable(item, $"需先开启「{parent.Title}」。");
                return;
            }
        }

        item.IsAvailable = true;
        item.DisabledReason = null;
    }

    private static void Disable(SettingItemViewModel item, string reason)
    {
        item.IsAvailable = false;
        item.DisabledReason = reason;
    }
}
