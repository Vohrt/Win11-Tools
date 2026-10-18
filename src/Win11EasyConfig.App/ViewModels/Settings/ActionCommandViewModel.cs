using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Win11EasyConfig.Core.Settings;

namespace Win11EasyConfig.App.ViewModels.Settings;

/// <summary>Presents an <see cref="IActionCommand"/> as a button with optional confirmation.</summary>
public sealed partial class ActionCommandViewModel : ObservableObject
{
    private readonly IActionCommand _command;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    public ActionCommandViewModel(IActionCommand command)
    {
        _command = command;
    }

    public string Id => _command.Id;

    public string Title => _command.Title;

    public string? ConfirmationMessage => _command.ConfirmationMessage;

    public bool RequiresConfirmation => !string.IsNullOrEmpty(_command.ConfirmationMessage);

    public bool IsDestructive => _command.IsDestructive;

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public async Task ExecuteAsync()
    {
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var result = await _command.ExecuteAsync().ConfigureAwait(true);
            StatusMessage = result.Success ? result.Message ?? "完成" : result.Message;
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

/// <summary>Groups action buttons under their heading (快速打开 / 资源管理器操作 …).</summary>
public sealed partial class ActionGroupViewModel : ObservableObject
{
    public ActionGroupViewModel(string groupName, IEnumerable<ActionCommandViewModel> commands)
    {
        GroupName = groupName;
        Commands = [.. commands];
    }

    public string GroupName { get; }

    public ObservableCollection<ActionCommandViewModel> Commands { get; }
}
