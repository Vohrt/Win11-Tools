using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11EasyConfig.Core.Features;

namespace Win11EasyConfig.App.ViewModels;

/// <summary>Backs the Edge / WebView2 management page.</summary>
public sealed partial class EdgeViewModel : ObservableObject
{
    private readonly EdgeService _edge;

    [ObservableProperty]
    public partial string StableStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BetaStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DevStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CanaryStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GlobalWebView2Status { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserWebView2Status { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool StartupBoostDisabled { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; set; }

    private bool _suppressBoostApply;

    public EdgeViewModel(EdgeService edge)
    {
        _edge = edge;
    }

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    [RelayCommand]
    public Task LoadAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task InstallStable() => RunAsync(() => _edge.InstallEdgeAsync(EdgeChannel.Stable), "已启动 Edge（稳定版）安装程序。");

    [RelayCommand]
    private Task InstallBeta() => RunAsync(() => _edge.InstallEdgeAsync(EdgeChannel.Beta), "已启动 Edge Beta 安装程序。");

    [RelayCommand]
    private Task InstallDev() => RunAsync(() => _edge.InstallEdgeAsync(EdgeChannel.Dev), "已启动 Edge Dev 安装程序。");

    [RelayCommand]
    private Task InstallCanary() => RunAsync(() => _edge.InstallEdgeAsync(EdgeChannel.Canary), "已启动 Edge Canary 安装程序。");

    [RelayCommand]
    private Task InstallWebView2() => RunAsync(() => _edge.InstallWebView2Async(), "已启动 WebView2 运行时安装程序。");

    [RelayCommand]
    private Task DisableAutoUpdate() =>
        RunAsync(() => _edge.DisableAutoUpdateAsync(), "已删除 Edge 更新服务与计划任务。");

    [RelayCommand]
    private Task UninstallGlobalWebView2() =>
        RunAsync(async () =>
        {
            var ok = await _edge.UninstallGlobalWebView2Async().ConfigureAwait(true);
            Message = ok ? "已卸载全局 WebView2 运行时。" : "未检测到全局 WebView2 运行时。";
        }, successMessage: null);

    partial void OnStartupBoostDisabledChanged(bool value)
    {
        if (_suppressBoostApply)
        {
            return;
        }

        _edge.SetStartupBoostDisabled(value);
        Message = value ? "已禁用 Edge 启动增强。" : "已恢复 Edge 启动增强默认设置。";
    }

    private async Task RunAsync(Func<Task> action, string? successMessage)
    {
        IsBusy = true;
        Message = null;
        try
        {
            await action().ConfigureAwait(true);
            if (successMessage is not null)
            {
                Message = successMessage;
            }
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
        finally
        {
            Refresh();
            IsBusy = false;
        }
    }

    private void Refresh()
    {
        try
        {
            var overview = _edge.GetOverview();
            StableStatus = overview.Stable.DisplayStatus;
            BetaStatus = overview.Beta.DisplayStatus;
            DevStatus = overview.Dev.DisplayStatus;
            CanaryStatus = overview.Canary.DisplayStatus;
            GlobalWebView2Status = overview.GlobalWebView2.DisplayStatus;
            UserWebView2Status = overview.UserWebView2.DisplayStatus;

            _suppressBoostApply = true;
            StartupBoostDisabled = overview.StartupBoostDisabled;
            _suppressBoostApply = false;
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}
