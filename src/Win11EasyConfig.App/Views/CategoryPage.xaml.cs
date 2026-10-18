using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Win11EasyConfig.App.ViewModels;
using Win11EasyConfig.App.ViewModels.Settings;
using Win11EasyConfig.Core.Models;

namespace Win11EasyConfig.App.Views;

/// <summary>Navigation payload describing which category the generic page should show.</summary>
public sealed record TweakPageParameter(TweakCategory Category, string Header, string? Subtitle);

public sealed partial class CategoryPage : Page
{
    public CategoryPage()
    {
        ViewModel = AppHost.GetRequiredService<CategoryPageViewModel>();
        InitializeComponent();
    }

    public CategoryPageViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is TweakPageParameter parameter)
        {
            await ViewModel.InitializeAsync(parameter.Category, parameter.Header, parameter.Subtitle);
        }
    }

    private async void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ActionCommandViewModel vm })
        {
            return;
        }

        if (vm.RequiresConfirmation)
        {
            var dialog = new ContentDialog
            {
                Title = "请确认",
                Content = vm.ConfirmationMessage,
                PrimaryButtonText = "继续",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        await vm.ExecuteAsync();
    }
}
