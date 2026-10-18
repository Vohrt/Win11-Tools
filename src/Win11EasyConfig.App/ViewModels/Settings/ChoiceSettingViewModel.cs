using CommunityToolkit.Mvvm.ComponentModel;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Settings;

namespace Win11EasyConfig.App.ViewModels.Settings;

/// <summary>Presents an <see cref="IChoiceSetting"/> as a labelled combo box.</summary>
public sealed partial class ChoiceSettingViewModel : SettingItemViewModel
{
    private readonly IChoiceSetting _choice;
    private bool _suppressApply;

    [ObservableProperty]
    public partial int SelectedIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public ChoiceSettingViewModel(IChoiceSetting choice)
    {
        _choice = choice;
        Options = [.. choice.Options.Select(o => o.Label)];
    }

    public override string Id => _choice.Id;

    public override string Title => _choice.Title;

    public IReadOnlyList<string> Options { get; }

    public string? Description => _choice.Description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(_choice.Description);

    public string EffectLabel => _choice.Effect switch
    {
        EffectScope.RestartExplorer => "需重启资源管理器",
        EffectScope.SignOut => "需注销后生效",
        EffectScope.Reboot => "需重启电脑",
        _ => "立即生效"
    };

    public bool ShowEffectBadge => _choice.Effect != EffectScope.Immediate;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public override async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var index = await _choice.GetSelectedIndexAsync(cancellationToken).ConfigureAwait(true);
            _suppressApply = true;
            SelectedIndex = index;
            _suppressApply = false;
        }
        catch
        {
            // Leave the current selection if the state cannot be read.
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (_suppressApply || value < 0)
        {
            return;
        }

        _ = ApplyAsync(value);
    }

    private async Task ApplyAsync(int index)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _choice.ApplyAsync(index).ConfigureAwait(true);
            if (!result.Success)
            {
                ErrorMessage = result.Message;
            }

            var actual = await _choice.GetSelectedIndexAsync().ConfigureAwait(true);
            _suppressApply = true;
            SelectedIndex = actual;
            _suppressApply = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
