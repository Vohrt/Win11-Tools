using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11EasyConfig.Core.Settings;

namespace Win11EasyConfig.App.ViewModels.Settings;

/// <summary>Presents an <see cref="INumericSetting"/> as a spin box plus an apply button.</summary>
public sealed partial class NumericSettingViewModel : SettingItemViewModel
{
    private readonly INumericSetting _numeric;

    [ObservableProperty]
    public partial double Value { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    public NumericSettingViewModel(INumericSetting numeric)
    {
        _numeric = numeric;
    }

    public override string Id => _numeric.Id;

    public override string Title => _numeric.Title;

    public override IReadOnlyList<string> RequiresOn => _numeric.RequiresOn;

    public string? Description => _numeric.Description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(_numeric.Description);

    public int Minimum => _numeric.Minimum;

    public int Maximum => _numeric.Maximum;

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public override async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            Value = await _numeric.GetValueAsync(cancellationToken).ConfigureAwait(true);
        }
        catch
        {
            // Keep the last value when the state cannot be read.
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var result = await _numeric.ApplyAsync((int)Math.Round(Value)).ConfigureAwait(true);
            StatusMessage = result.Success ? "已应用" : result.Message;
            Value = await _numeric.GetValueAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
