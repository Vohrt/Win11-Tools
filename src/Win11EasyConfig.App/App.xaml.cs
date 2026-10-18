using Microsoft.UI.Xaml;

namespace Win11EasyConfig.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        AppHost.Initialize();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
