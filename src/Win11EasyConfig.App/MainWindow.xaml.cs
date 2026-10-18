using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Win11EasyConfig.App.Views;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Services;

namespace Win11EasyConfig.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "Windows11 轻松设置";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppTitleText.Text = WindowsInfoService.IsElevated()
            ? "Windows11 轻松设置"
            : "Windows11 轻松设置（未提权，部分设置将失败）";

        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        switch (tag)
        {
            case "SystemInfo":
                ContentFrame.Navigate(typeof(SystemInfoPage), null, new EntranceNavigationTransitionInfo());
                return;
            case "Edge":
                ContentFrame.Navigate(typeof(EdgePage), null, new EntranceNavigationTransitionInfo());
                return;
            case "Apps":
                ContentFrame.Navigate(typeof(AppsPage), null, new EntranceNavigationTransitionInfo());
                return;
        }

        if (!Enum.TryParse<TweakCategory>(tag, out var category))
        {
            return;
        }

        var parameter = new TweakPageParameter(category, DescribeCategory(category), DescribeSubtitle(category));
        ContentFrame.Navigate(typeof(CategoryPage), parameter, new EntranceNavigationTransitionInfo());
    }

    private static string DescribeCategory(TweakCategory category) => category switch
    {
        TweakCategory.System => "系统设置",
        TweakCategory.Advanced => "高级设置",
        TweakCategory.Explorer => "资源管理器",
        TweakCategory.Taskbar => "任务栏",
        TweakCategory.Privacy => "隐私",
        TweakCategory.Performance => "性能与远程",
        TweakCategory.Network => "网络",
        _ => category.ToString()
    };

    private static string DescribeSubtitle(TweakCategory category) => category switch
    {
        TweakCategory.System => "还原点、更新、遥测与诊断服务等日常开关。",
        TweakCategory.Advanced => "安全缓解与启动级设置，多数需要重启，请谨慎更改。",
        TweakCategory.Explorer => "文件资源管理器的显示方式与图标外观。",
        TweakCategory.Taskbar => "任务栏上的按钮与时钟显示。",
        TweakCategory.Privacy => "搜索、遥测与个性化相关的隐私开关。",
        TweakCategory.Performance => "休眠、内存管理与远程访问。",
        TweakCategory.Network => "网络协议与拥塞控制。",
        _ => string.Empty
    };
}
