using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Catalog;

/// <summary>
/// "系统设置" — everyday switches for restore points, updates, telemetry and diagnostics.
/// </summary>
internal static class SystemTweaks
{
    private const string SystemRestoreKey = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows NT\SystemRestore";
    private const string WindowsSearchPolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search";
    private const string DshKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Dsh";
    private const string WindowsUpdatePolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string SqmClientKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\SQMClient\Windows";
    private const string UpdateUxKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private const string PauseStart = "2023-05-08T09:59:52Z";
    private const string PauseEnd = "2042-07-07T09:59:52Z";

    public static IEnumerable<ITweak> Create(TweakFactory factory, IWindowsServiceManager services)
    {
        yield return factory.Custom(
            "system.restore",
            "系统还原",
            TweakCategory.System,
            reg => (reg.GetInt(SystemRestoreKey, "DisableSR") ?? -1) <= 0 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Delete(SystemRestoreKey, "DisableSR")],
            [RegistryWrite.Int(SystemRestoreKey, "DisableSR", 1)],
            description: "允许系统创建还原点。关闭后可节省磁盘空间，但无法通过还原点回滚系统。");

        yield return factory.Policy(
            "system.searchHighlights",
            "搜索界面显示搜索热点",
            TweakCategory.System,
            WindowsSearchPolicyKey,
            "EnableDynamicContentInWSB",
            offValue: 0,
            description: "关闭后，搜索框与搜索面板不再显示微软推送的热点内容。");

        yield return factory.Policy(
            "system.newsAndInterests",
            "小组件（新闻和兴趣）",
            TweakCategory.System,
            DshKey,
            "AllowNewsAndInterests",
            offValue: 0,
            description: "控制任务栏上的小组件面板是否可用。",
            effect: EffectScope.SignOut);

        yield return factory.Custom(
            "system.wuDrivers",
            "通过 Windows 更新自动更新硬件驱动",
            TweakCategory.System,
            reg => (reg.GetInt(WindowsUpdatePolicyKey, "ExcludeWUDriversInQualityUpdate") ?? -1) <= 0
                ? TweakState.On
                : TweakState.Off,
            [RegistryWrite.Delete(WindowsUpdatePolicyKey, "ExcludeWUDriversInQualityUpdate")],
            [RegistryWrite.Int(WindowsUpdatePolicyKey, "ExcludeWUDriversInQualityUpdate", 1)],
            description: "关闭后 Windows 更新不再自动安装硬件驱动程序，避免驱动被意外覆盖。");

        yield return factory.Policy(
            "system.ceip",
            "微软客户体验改善计划（CEIP）",
            TweakCategory.System,
            SqmClientKey,
            "CEIPEnable",
            offValue: 0,
            description: "普通用户建议关闭，可减少后台遥测数据上传。");

        yield return factory.Custom(
            "system.pauseUpdates",
            "接收 Windows 更新",
            TweakCategory.System,
            reg => (reg.GetString(UpdateUxKey, "PauseUpdatesExpiryTime")?.StartsWith("2042", StringComparison.Ordinal) ?? false)
                ? TweakState.Off
                : TweakState.On,
            onWrites:
            [
                RegistryWrite.Delete(UpdateUxKey, "FlightSettingsMaxPauseDays"),
                RegistryWrite.Delete(UpdateUxKey, "PauseFeatureUpdatesStartTime"),
                RegistryWrite.Delete(UpdateUxKey, "PauseFeatureUpdatesEndTime"),
                RegistryWrite.Delete(UpdateUxKey, "PauseQualityUpdatesStartTime"),
                RegistryWrite.Delete(UpdateUxKey, "PauseQualityUpdatesEndTime"),
                RegistryWrite.Delete(UpdateUxKey, "PauseUpdatesStartTime"),
                RegistryWrite.Delete(UpdateUxKey, "PauseUpdatesExpiryTime")
            ],
            offWrites:
            [
                RegistryWrite.String(UpdateUxKey, "PauseFeatureUpdatesStartTime", PauseStart),
                RegistryWrite.String(UpdateUxKey, "PauseFeatureUpdatesEndTime", PauseEnd),
                RegistryWrite.String(UpdateUxKey, "PauseQualityUpdatesStartTime", PauseStart),
                RegistryWrite.String(UpdateUxKey, "PauseQualityUpdatesEndTime", PauseEnd),
                RegistryWrite.String(UpdateUxKey, "PauseUpdatesStartTime", PauseStart),
                RegistryWrite.String(UpdateUxKey, "PauseUpdatesExpiryTime", PauseEnd),
                RegistryWrite.Int(UpdateUxKey, "FlightSettingsMaxPauseDays", 7000)
            ],
            description: "关闭后将把 Windows 更新暂停至 2042 年，相当于长期停止接收更新。");

        yield return new ServiceTweak(
            "system.pcaSvc",
            "程序兼容性助手服务（PcaSvc）",
            TweakCategory.System,
            services,
            "PcaSvc")
        {
            Description = "监视程序安装与运行并检测兼容性问题。普通用户可以禁用。"
        };

        yield return new ServiceTweak(
            "system.dps",
            "诊断策略服务（DPS）",
            TweakCategory.System,
            services,
            "DPS")
        {
            Description = "提供 Windows 组件的问题检测与疑难解答。普通用户可以禁用。"
        };
    }
}
