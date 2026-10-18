using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Win11EasyConfig.App.ViewModels;

namespace Win11EasyConfig.App.Views;

public sealed partial class SystemInfoPage : Page
{
    public SystemInfoPage()
    {
        InitializeComponent();
        ViewModel = AppHost.GetRequiredService<SystemInfoViewModel>();
    }

    public SystemInfoViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }
}
