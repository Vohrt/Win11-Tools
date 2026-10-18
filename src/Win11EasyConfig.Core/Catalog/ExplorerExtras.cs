using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Services;
using Win11EasyConfig.Core.Settings;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Catalog;

/// <summary>
/// The multi-choice settings and one-shot buttons for the Explorer and Taskbar categories.
/// </summary>
internal static class ExplorerExtras
{
    private const string AdvancedKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string SearchKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search";
    private const string ClassicMenuKey = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

    public static IEnumerable<IChoiceSetting> CreateChoices(
        IRegistryService registry,
        ShellService shell)
    {
        yield return new RegistryChoiceSetting(
            "explorer.launchTo", "打开资源管理器时打开", TweakCategory.Explorer, registry,
            AdvancedKey, "LaunchTo",
            [new ChoiceOption("主文件夹", 0), new ChoiceOption("此电脑", 1)],
            fallbackIndex: 0,
            deleteValueForIndex: 0);

        yield return new DelegateChoiceSetting(
            "explorer.rightClickMenu", "右键菜单风格", TweakCategory.Explorer,
            [new ChoiceOption("Win11 现代菜单", 0), new ChoiceOption("Win10 经典菜单", 1)],
            read: () => Task.FromResult(
                registry.GetString($@"{ClassicMenuKey}\InprocServer32", string.Empty) is not null ? 1 : 0),
            write: index =>
            {
                try
                {
                    if (index == 1)
                    {
                        registry.SetString($@"{ClassicMenuKey}\InprocServer32", string.Empty, string.Empty);
                    }
                    else
                    {
                        registry.DeleteKeyTree(ClassicMenuKey);
                    }

                    shell.RestartExplorer();
                    return Task.FromResult(TweakApplyResult.Ok());
                }
                catch (Exception ex)
                {
                    return Task.FromResult(TweakApplyResult.Fail(ex.Message));
                }
            })
        {
            Description = "切换 Win11 的精简右键菜单与 Win10 的完整经典菜单。",
            Effect = EffectScope.RestartExplorer
        };

        yield return new RegistryChoiceSetting(
            "taskbar.searchBox", "搜索按钮", TweakCategory.Taskbar, registry,
            SearchKey, "SearchboxTaskbarMode",
            [
                new ChoiceOption("隐藏", 0),
                new ChoiceOption("搜索图标", 1),
                new ChoiceOption("搜索框", 2),
                new ChoiceOption("搜索图标和标签", 3)
            ],
            fallbackIndex: 2);

        yield return new RegistryChoiceSetting(
            "taskbar.alignment", "任务栏对齐", TweakCategory.Taskbar, registry,
            AdvancedKey, "TaskbarAl",
            [new ChoiceOption("靠左", 0), new ChoiceOption("居中", 1)],
            fallbackIndex: 1);

        yield return new RegistryChoiceSetting(
            "taskbar.buttonMerge", "任务栏按钮合并", TweakCategory.Taskbar, registry,
            AdvancedKey, "TaskbarGlomLevel",
            [
                new ChoiceOption("始终合并", 0),
                new ChoiceOption("任务栏已满时合并", 1),
                new ChoiceOption("从不合并", 2)
            ],
            fallbackIndex: 0,
            afterApply: () => { shell.RestartExplorer(); return Task.CompletedTask; })
        {
            Effect = EffectScope.RestartExplorer
        };

        yield return new RegistryChoiceSetting(
            "privacy.startMenuRows", "开始菜单推荐的项目显示行数", TweakCategory.Privacy, registry,
            AdvancedKey, "Start_Layout",
            [new ChoiceOption("三行（默认）", 0), new ChoiceOption("一行", 1), new ChoiceOption("四行", 2)],
            fallbackIndex: 0);
    }

    public static IEnumerable<IActionCommand> CreateActions(ICommandRunner runner, ShellService shell)
    {
        // --- Explorer maintenance ---
        yield return new ActionCommand("action.refreshIconCache", "刷新系统图标缓存", TweakCategory.Explorer, "资源管理器操作",
            _ => { shell.RefreshIconCache(); return Task.FromResult(new TweakApplyResult(true, "已通知系统刷新图标缓存。")); });

        yield return new ActionCommand("action.restartExplorer", "重启文件资源管理器", TweakCategory.Explorer, "资源管理器操作",
            _ => { shell.RestartExplorer(); return Task.FromResult(new TweakApplyResult(true, "文件资源管理器已重启。")); })
        {
            ConfirmationMessage = "确定要重启文件资源管理器吗？桌面与任务栏会短暂消失。"
        };

        yield return new ActionCommand("action.emptyRecycleBin", "清空回收站", TweakCategory.Explorer, "资源管理器操作",
            _ => { shell.EmptyRecycleBin(); return Task.FromResult(new TweakApplyResult(true, "回收站已清空。")); })
        {
            ConfirmationMessage = "确定要清空回收站吗？此操作无法撤销。",
            IsDestructive = true
        };

        yield return new ActionCommand("action.performanceOptions", "性能选项…", TweakCategory.Explorer, "资源管理器操作",
            _ => { runner.Launch("SystemPropertiesPerformance.exe"); return Task.FromResult(TweakApplyResult.Ok()); });

        yield return new ActionCommand("action.desktopIcons", "桌面图标设置…", TweakCategory.Explorer, "资源管理器操作",
            _ => { runner.Launch("rundll32.exe", "shell32.dll,Control_RunDLL desk.cpl,,0"); return Task.FromResult(TweakApplyResult.Ok()); });

        // --- Quick launch shortcuts (原“快速打开”) ---
        foreach (var (id, title, file, args) in new (string, string, string, string?)[]
        {
            ("action.openSettings", "系统设置", "ms-settings:", null),
            ("action.openControlPanel", "控制面板", "control", null),
            ("action.openPrograms", "程序和功能", "appwiz.cpl", null),
            ("action.openComputerMgmt", "计算机管理", "compmgmt.msc", null),
            ("action.openNetwork", "网络连接", "ncpa.cpl", null),
            ("action.openPower", "电源选项", "powercfg.cpl", null),
            ("action.openDeviceMgr", "设备管理器", "devmgmt.msc", null),
            ("action.openOsk", "屏幕键盘", "osk.exe", null),
            ("action.openDiskMgmt", "磁盘管理", "diskmgmt.msc", null),
            ("action.openGodMode", "上帝模式", "explorer.exe", "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}")
        })
        {
            var capturedFile = file;
            var capturedArgs = args;
            yield return new ActionCommand(id, title, TweakCategory.Explorer, "快速打开",
                _ => { runner.Launch(capturedFile, capturedArgs); return Task.FromResult(TweakApplyResult.Ok()); });
        }
    }
}
