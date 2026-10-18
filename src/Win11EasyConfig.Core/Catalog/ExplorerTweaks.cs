using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Services;
using Win11EasyConfig.Core.Settings;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Catalog;

/// <summary>
/// "Explorer 设置" — file explorer appearance and behaviour, plus the taskbar switches.
/// </summary>
internal static class ExplorerTweaks
{
    private const string AdvancedKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ExplorerKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer";
    private const string CabinetStateKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState";
    private const string ShellIconsKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
    private const string OneDrivePolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\OneDrive";
    private const string BlankIcon = @"%systemroot%\system32\imageres.dll,197";

    private const string ItemsViewAdapterKey = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{2aa9162e-c906-4dd9-ad0b-3d24a8eef5a0}";
    private const string XamlIslandAdapterKey = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{6480100b-5a83-4d1e-9f69-8ae5a88e9a33}";

    public static IEnumerable<ITweak> Create(TweakFactory factory, IRegistryService registry, WindowsInfoService windowsInfo)
    {
        // Windows 11 21H2 (build 22000) cannot use the Win10 explorer shell, so the option is
        // reported as unsupported there.
        var build = windowsInfo.GetVersion().CurrentBuild;
        var win10StyleSupported = build != 22000;

        // ---- File explorer ----
        yield return factory.Value(
            "explorer.showFileExtensions", "显示所有文件的文件扩展名", TweakCategory.Explorer,
            AdvancedKey, "HideFileExt", onValue: 0, offValue: 1, defaultWhenMissing: 1,
            description: "隐藏扩展名容易被伪装成图片的可执行文件欺骗，建议开启。");

        yield return factory.Value(
            "explorer.showFullPath", "在标题栏中显示完整路径", TweakCategory.Explorer,
            CabinetStateKey, "FullPath", onValue: 1, offValue: 0, defaultWhenMissing: 0);

        yield return factory.Value(
            "explorer.showHiddenFiles", "显示隐藏的文件、文件夹和驱动器", TweakCategory.Explorer,
            AdvancedKey, "Hidden", onValue: 1, offValue: 2, defaultWhenMissing: 2);

        yield return factory.Value(
            "explorer.hideProtectedSystemFiles", "隐藏受保护的操作系统文件", TweakCategory.Explorer,
            AdvancedKey, "ShowSuperHidden", onValue: 0, offValue: 1, defaultWhenMissing: 0,
            description: "推荐保持开启，避免误删系统文件。");

        yield return factory.Value(
            "explorer.iconsOnly", "始终显示图标，从不显示缩略图", TweakCategory.Explorer,
            AdvancedKey, "IconsOnly", onValue: 1, offValue: 0, defaultWhenMissing: 0);

        yield return factory.Value(
            "explorer.showEmptyDrives", "显示空的驱动器", TweakCategory.Explorer,
            AdvancedKey, "HideDrivesWithNoMedia", onValue: 0, offValue: 1, defaultWhenMissing: 1);

        yield return factory.Value(
            "explorer.showRecentFiles", "在快速访问中显示最近使用的文件", TweakCategory.Explorer,
            ExplorerKey, "ShowRecent", onValue: 1, offValue: 0, defaultWhenMissing: 1);

        yield return factory.Value(
            "explorer.showFrequentFolders", "在快速访问中显示常用文件夹", TweakCategory.Explorer,
            ExplorerKey, "ShowFrequent", onValue: 1, offValue: 0, defaultWhenMissing: 1);

        yield return factory.Value(
            "explorer.showOfficeFiles", "显示来自 office.com 的文件", TweakCategory.Explorer,
            ExplorerKey, "ShowCloudFilesInQuickAccess", onValue: 1, offValue: 0, defaultWhenMissing: 1);

        yield return factory.Custom(
            "explorer.removeShortcutArrow", "去除快捷方式的小箭头", TweakCategory.Explorer,
            reg => reg.GetString(ShellIconsKey, "29") == BlankIcon ? TweakState.On : TweakState.Off,
            [RegistryWrite.String(ShellIconsKey, "29", BlankIcon)],
            [RegistryWrite.Delete(ShellIconsKey, "29")],
            effect: EffectScope.RestartExplorer);

        yield return factory.Custom(
            "explorer.removeShieldIcon", "去除程序图标的盾牌标识", TweakCategory.Explorer,
            reg => reg.GetString(ShellIconsKey, "77") == BlankIcon ? TweakState.On : TweakState.Off,
            [RegistryWrite.String(ShellIconsKey, "77", BlankIcon)],
            [RegistryWrite.Delete(ShellIconsKey, "77")],
            effect: EffectScope.RestartExplorer);

        yield return factory.Custom(
            "explorer.noShortcutText", "创建快捷方式时不添加“快捷方式”文字", TweakCategory.Explorer,
            reg =>
            {
                var value = reg.GetBinary(ExplorerKey, "link");
                return value is { Length: 4 } && value.All(b => b == 0) ? TweakState.On : TweakState.Off;
            },
            [RegistryWrite.Binary(ExplorerKey, "link", new byte[4])],
            [RegistryWrite.Delete(ExplorerKey, "link")],
            effect: EffectScope.RestartExplorer);

        yield return factory.Custom(
            "explorer.disableOneDrive", "禁止使用 OneDrive", TweakCategory.Explorer,
            reg => (reg.GetInt(OneDrivePolicyKey, "DisableFileSyncNGSC") ?? 0) >= 1 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Int(OneDrivePolicyKey, "DisableFileSyncNGSC", 1)],
            [RegistryWrite.Delete(OneDrivePolicyKey, "DisableFileSyncNGSC")],
            description: "通过组策略禁用 OneDrive 文件同步。",
            effect: EffectScope.SignOut);

        yield return factory.Custom(
            "explorer.win10Style", "使用 Win10 资源管理器风格", TweakCategory.Explorer,
            reg =>
            {
                var a = reg.GetString($@"{ItemsViewAdapterKey}\InProcServer32", "ThreadingModel");
                var b = reg.GetString($@"{XamlIslandAdapterKey}\InProcServer32", "ThreadingModel");
                return a == "Apartment" && b == "Apartment" ? TweakState.On : TweakState.Off;
            },
            onWrites:
            [
                RegistryWrite.String(ItemsViewAdapterKey, string.Empty, "CLSID_ItemsViewAdapter"),
                RegistryWrite.String($@"{ItemsViewAdapterKey}\InProcServer32", string.Empty, @"C:\Windows\System32\Windows.UI.FileExplorer.dll_"),
                RegistryWrite.String($@"{ItemsViewAdapterKey}\InProcServer32", "ThreadingModel", "Apartment"),
                RegistryWrite.String(XamlIslandAdapterKey, string.Empty, "File Explorer Xaml Island View Adapter"),
                RegistryWrite.String($@"{XamlIslandAdapterKey}\InProcServer32", string.Empty, @"C:\Windows\System32\Windows.UI.FileExplorer.dll_"),
                RegistryWrite.String($@"{XamlIslandAdapterKey}\InProcServer32", "ThreadingModel", "Apartment")
            ],
            offWrites:
            [
                RegistryWrite.DeleteTree(ItemsViewAdapterKey),
                RegistryWrite.DeleteTree(XamlIslandAdapterKey)
            ],
            description: "把 Win11 的资源管理器界面切换回 Win10 风格。",
            effect: EffectScope.RestartExplorer,
            isSupported: win10StyleSupported,
            unsupportedReason: "当前系统版本（Windows 11 21H2 / 内部版本 22000）不支持此项。");

        // ---- Taskbar ----
        yield return factory.Value(
            "taskbar.taskView", "显示“任务视图”按钮", TweakCategory.Taskbar,
            AdvancedKey, "ShowTaskViewButton", onValue: 1, offValue: 0, defaultWhenMissing: 1);

        yield return factory.Value(
            "taskbar.chat", "显示“聊天”按钮", TweakCategory.Taskbar,
            AdvancedKey, "TaskbarMn", onValue: 1, offValue: 0, defaultWhenMissing: 1);

        yield return factory.Value(
            "taskbar.widgets", "显示“小组件”按钮", TweakCategory.Taskbar,
            AdvancedKey, "TaskbarDa", onValue: 1, offValue: 0, defaultWhenMissing: 1);

        yield return new AutoHideTaskbarTweak(registry)
        {
            Description = "任务栏将自动隐藏，仅当鼠标指向屏幕边缘时才显示。",
            Effect = EffectScope.RestartExplorer
        };

        yield return factory.Value(
            "taskbar.secondsInClock", "系统托盘时间显示秒", TweakCategory.Taskbar,
            AdvancedKey, "ShowSecondsInSystemClock", onValue: 1, offValue: 0, defaultWhenMissing: 0);

        yield return factory.Custom(
            "taskbar.copilot", "显示 Copilot 按钮", TweakCategory.Taskbar,
            reg => reg.GetInt(AdvancedKey, "ShowCopilotButton") == 0 ? TweakState.Off : TweakState.On,
            [RegistryWrite.Delete(AdvancedKey, "ShowCopilotButton")],
            [RegistryWrite.Int(AdvancedKey, "ShowCopilotButton", 0)]);
    }
}

/// <summary>
/// "自动隐藏任务栏". The flag lives in the StuckRects3 "Settings" binary blob: bit0 of byte 8
/// is the auto-hide flag. Toggling requires flipping that single bit in place, which the
/// declarative <see cref="RegistryTweak"/> cannot express because the new blob depends on the
/// current one, hence this dedicated tweak.
internal sealed class AutoHideTaskbarTweak(IRegistryService registry)
    : TweakBase("taskbar.autoHide", "自动隐藏任务栏", TweakCategory.Taskbar)
{
    private const string StuckRectsKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";
    private const int AutoHideByteIndex = 8;

    public override Task<TweakState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var settings = registry.GetBinary(StuckRectsKey, "Settings");
        if (settings is null || settings.Length <= AutoHideByteIndex)
        {
            return Task.FromResult(TweakState.Unknown);
        }

        return Task.FromResult(settings[AutoHideByteIndex] % 2 != 0 ? TweakState.On : TweakState.Off);
    }

    public override Task<TweakApplyResult> ApplyAsync(bool enable, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = registry.GetBinary(StuckRectsKey, "Settings");
            if (settings is null || settings.Length <= AutoHideByteIndex)
            {
                return Task.FromResult(TweakApplyResult.Fail(
                    @"无法读取任务栏设置（StuckRects3\Settings），无法修改自动隐藏状态。"));
            }

            var current = settings[AutoHideByteIndex];
            if (current % 2 != 0 == enable)
            {
                // Only write when the byte actually changes.
                return Task.FromResult(TweakApplyResult.Ok());
            }

            settings[AutoHideByteIndex] = (byte)(enable ? current + 1 : current - 1);
            registry.SetBinary(StuckRectsKey, "Settings", settings);
            return Task.FromResult(TweakApplyResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakApplyResult.Fail(ex.Message));
        }
    }
}
