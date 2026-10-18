using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Win11EasyConfig.Core.Features;

namespace Win11EasyConfig.App.ViewModels;

/// <summary>
/// One app row. Its status pill and the availability of the install/uninstall buttons are driven
/// entirely by the detected state for the active scope, so the two actions are never both enabled.
/// </summary>
public sealed partial class StoreAppItemViewModel : ObservableObject
{
    private readonly AppsViewModel _parent;

    // StatusKind: 0 = neutral/absent, 1 = installed (green), 2 = provisioned (purple).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGreen))]
    [NotifyPropertyChangedFor(nameof(ShowPurple))]
    [NotifyPropertyChangedFor(nameof(ShowNeutral))]
    public partial int StatusKind { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "检测中…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallEnabled))]
    public partial bool CanInstall { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UninstallEnabled))]
    public partial bool CanUninstall { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallEnabled))]
    public partial bool ShowInstall { get; set; } = true;

    [ObservableProperty]
    public partial string UninstallLabel { get; set; } = "卸载";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallEnabled))]
    [NotifyPropertyChangedFor(nameof(UninstallEnabled))]
    public partial bool IsBusy { get; set; }

    public StoreAppItemViewModel(AppsViewModel parent, StoreApp app)
    {
        _parent = parent;
        App = app;
        AvatarBrush = CreateAvatarBrush(app.Name);
    }

    public StoreApp App { get; }

    public string Name => App.Name;

    public string? Description => App.Description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(App.Description);

    /// <summary>First character shown in the round avatar (no per-app art is bundled).</summary>
    public string AvatarText => App.Name.Length > 0 ? App.Name[..1].ToUpperInvariant() : "?";

    public SolidColorBrush AvatarBrush { get; }

    private static readonly Color[] AvatarPalette =
    [
        Color.FromArgb(255, 0x0F, 0x6C, 0xBD), // blue
        Color.FromArgb(255, 0x7A, 0x34, 0xC9), // purple
        Color.FromArgb(255, 0x0F, 0x7B, 0x0F), // green
        Color.FromArgb(255, 0xCA, 0x50, 0x10), // orange
        Color.FromArgb(255, 0x03, 0x83, 0x87), // teal
        Color.FromArgb(255, 0x98, 0x6F, 0x0B), // gold
        Color.FromArgb(255, 0xB4, 0x00, 0x9E), // magenta
        Color.FromArgb(255, 0x4F, 0x6B, 0xED)  // indigo
    ];

    /// <summary>FNV-1a over the name, so an app keeps the same avatar color across runs.</summary>
    private static SolidColorBrush CreateAvatarBrush(string name)
    {
        var hash = 2166136261u;
        foreach (var ch in name)
        {
            hash = (hash ^ ch) * 16777619u;
        }

        return new SolidColorBrush(AvatarPalette[hash % (uint)AvatarPalette.Length]);
    }

    public bool ShowGreen => StatusKind == 1;

    public bool ShowPurple => StatusKind == 2;

    public bool ShowNeutral => StatusKind == 0;

    public bool InstallEnabled => ShowInstall && CanInstall && !IsBusy;

    public bool UninstallEnabled => CanUninstall && !IsBusy;

    public void SetCurrentUserState(bool installed)
    {
        ShowInstall = App.CanInstallFromStore;
        UninstallLabel = "卸载";
        if (installed)
        {
            StatusKind = 1;
            StatusText = "已安装";
            CanInstall = false;
            CanUninstall = true;
        }
        else
        {
            StatusKind = 0;
            StatusText = "未安装";
            CanInstall = true;
            CanUninstall = false;
        }
    }

    public void SetSystemState(bool provisioned)
    {
        ShowInstall = false;
        UninstallLabel = "移除预配置";
        if (provisioned)
        {
            StatusKind = 2;
            StatusText = "已预配置";
            CanInstall = false;
            CanUninstall = true;
        }
        else
        {
            StatusKind = 0;
            StatusText = "未预配置";
            CanInstall = false;
            CanUninstall = false;
        }
    }

    [RelayCommand]
    private void Install() => _parent.InstallApp(this);

    /// <summary>Runs the actual removal (confirmation is handled by the page).</summary>
    public async Task ExecuteUninstallAsync()
    {
        IsBusy = true;
        try
        {
            await _parent.UninstallAppAsync(this).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>
/// Backs the Store-app management page: two scopes (当前用户应用管理 / 系统预装应用卸载)
/// plus the install options that apply to each.
/// </summary>
public sealed partial class AppsViewModel : ObservableObject
{
    private readonly AppsService _apps;

    // 0 = current user, 1 = system (provisioned).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrentUserScope))]
    [NotifyPropertyChangedFor(nameof(ScopeDescription))]
    public partial int SelectedScopeIndex { get; set; }

    [ObservableProperty]
    public partial bool UseWinGet { get; set; }

    [ObservableProperty]
    public partial bool AlsoRemoveProvisioned { get; set; }

    [ObservableProperty]
    public partial bool AskBeforeUninstall { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public AppsViewModel(AppsService apps)
    {
        _apps = apps;
    }

    public ObservableCollection<StoreAppItemViewModel> Apps { get; } = [];

    public AppScope Scope => SelectedScopeIndex == 1 ? AppScope.System : AppScope.CurrentUser;

    public bool IsCurrentUserScope => SelectedScopeIndex != 1;

    public string ScopeDescription => IsCurrentUserScope
        ? "管理当前登录用户的应用：绿色表示已为当前用户安装，可直接卸载；灰色表示未安装，可从商店或 WinGet 安装。"
        : "管理系统预装（预配置）应用：紫色表示会在新用户登录时自动安装，可移除其预配置。移除仅影响新用户且不可恢复，不会卸载当前用户已安装的相同应用。";

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (Apps.Count == 0)
        {
            foreach (var app in AppsService.Catalog)
            {
                Apps.Add(new StoreAppItemViewModel(this, app));
            }
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            if (Scope == AppScope.CurrentUser)
            {
                var installed = await _apps.GetInstalledPackagesAsync().ConfigureAwait(true);
                foreach (var item in Apps)
                {
                    item.SetCurrentUserState(AppsService.IsPresent(item.App, installed));
                }
            }
            else
            {
                var provisioned = _apps.GetProvisionedPackages();
                foreach (var item in Apps)
                {
                    item.SetSystemState(AppsService.IsPresent(item.App, provisioned));
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void InstallApp(StoreAppItemViewModel item) => _apps.Install(item.App, UseWinGet);

    public async Task UninstallAppAsync(StoreAppItemViewModel item)
    {
        if (Scope == AppScope.CurrentUser)
        {
            await _apps.UninstallForUserAsync(item.App, AlsoRemoveProvisioned).ConfigureAwait(true);
        }
        else
        {
            await _apps.RemoveProvisionedAsync(item.App).ConfigureAwait(true);
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Returns the confirmation text for an uninstall, or null to proceed without asking.</summary>
    public string? BuildUninstallPrompt(StoreAppItemViewModel item)
    {
        if (Scope == AppScope.System)
        {
            return $"确定要移除系统预装应用「{item.Name}」吗？\n\n" +
                   "预装应用会在新用户首次登录时自动安装。移除后：\n" +
                   "· 仅影响新用户，且无法恢复为“预装”状态；\n" +
                   "· 不会卸载当前用户已安装的相同应用；\n" +
                   "· 可释放其占用的磁盘空间。";
        }

        return AskBeforeUninstall ? $"确定要卸载应用「{item.Name}」吗？" : null;
    }
}
