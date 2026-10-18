using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Win11EasyConfig.App.ViewModels;

namespace Win11EasyConfig.App.Views;

public sealed partial class EdgePage : Page
{
    public EdgePage()
    {
        InitializeComponent();
        ViewModel = AppHost.GetRequiredService<EdgeViewModel>();
    }

    public EdgeViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }
}
