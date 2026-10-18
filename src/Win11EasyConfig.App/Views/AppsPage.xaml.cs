using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Win11EasyConfig.App.ViewModels;

namespace Win11EasyConfig.App.Views;

public sealed partial class AppsPage : Page
{
    public AppsPage()
    {
        ViewModel = AppHost.GetRequiredService<AppsViewModel>();
        InitializeComponent();
    }

    public AppsViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void OnScopeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null || ScopeSelector.SelectedIndex < 0)
        {
            return;
        }

        ViewModel.SelectedScopeIndex = ScopeSelector.SelectedIndex;
        await ViewModel.RefreshAsync();
    }

    private async void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StoreAppItemViewModel item })
        {
            return;
        }

        var prompt = ViewModel.BuildUninstallPrompt(item);
        if (prompt is not null)
        {
            var dialog = new ContentDialog
            {
                Title = "确认卸载",
                Content = prompt,
                PrimaryButtonText = "卸载",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        await item.ExecuteUninstallAsync();
    }
}
