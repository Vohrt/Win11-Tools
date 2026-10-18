using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Catalog;

/// <summary>
/// "高级设置" — security mitigations and boot-level switches. These mostly require a reboot and
/// trade security for performance, so every entry carries an explicit description.
/// </summary>
internal static class AdvancedTweaks
{
    private const string MemoryManagementKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
    private const string HvciKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
    private const string CiPolicyKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\CI\Policy";
    private const string DeviceGuardPolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeviceGuard";
    private const string DeviceGuardControlKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string PoliciesSystemKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

    public static IEnumerable<ITweak> Create(TweakFactory factory, ICommandRunner runner)
    {
        yield return factory.Custom(
            "advanced.speculativeMitigations",
            "Meltdown / Spectre 漏洞缓解",
            TweakCategory.Advanced,
            reg =>
            {
                var featureSettings = reg.GetInt(MemoryManagementKey, "FeatureSettings") ?? -1;
                var overrideValue = reg.GetInt(MemoryManagementKey, "FeatureSettingsOverride") ?? -1;
                var overrideMask = reg.GetInt(MemoryManagementKey, "FeatureSettingsOverrideMask") ?? -1;
                return featureSettings >= 1 && overrideValue == 3 && overrideMask == 3
                    ? TweakState.Off
                    : TweakState.On;
            },
            onWrites:
            [
                RegistryWrite.Delete(MemoryManagementKey, "FeatureSettingsOverride"),
                RegistryWrite.Delete(MemoryManagementKey, "FeatureSettingsOverrideMask"),
                RegistryWrite.Int(MemoryManagementKey, "FeatureSettings", 0)
            ],
            offWrites:
            [
                RegistryWrite.Int(MemoryManagementKey, "FeatureSettingsOverride", 3),
                RegistryWrite.Int(MemoryManagementKey, "FeatureSettingsOverrideMask", 3),
                RegistryWrite.Int(MemoryManagementKey, "FeatureSettings", 1)
            ],
            description: "关闭可提升 CPU 性能，但会降低系统安全性。请自行权衡。",
            effect: EffectScope.Reboot);

        yield return factory.Policy(
            "advanced.hvci",
            "内存完整性（HVCI）",
            TweakCategory.Advanced,
            HvciKey,
            "Enabled",
            offValue: 0,
            description: "运行 VMware 等虚拟化软件时通常必须关闭，否则虚拟化引擎无法工作。",
            effect: EffectScope.Reboot);

        yield return factory.Policy(
            "advanced.wdac",
            "智能应用控制 / WDAC 评估模式",
            TweakCategory.Advanced,
            CiPolicyKey,
            "VerifiedAndReputablePolicyState",
            offValue: 0,
            description: "Win11 22H2 起默认以评估模式启用，可能导致大型程序卡顿。普通用户建议关闭。",
            effect: EffectScope.Reboot);

        yield return factory.Custom(
            "advanced.vbs",
            "基于虚拟化的安全性（VBS）",
            TweakCategory.Advanced,
            reg =>
            {
                var policyVbs = reg.GetInt(DeviceGuardPolicyKey, "EnableVirtualizationBasedSecurity");
                var lsaCfgFlags = reg.GetInt(DeviceGuardPolicyKey, "LsaCfgFlags");
                var controlVbs = reg.GetInt(DeviceGuardControlKey, "EnableVirtualizationBasedSecurity");
                return policyVbs == 0 && lsaCfgFlags == 0 && controlVbs == 0 ? TweakState.Off : TweakState.On;
            },
            onWrites:
            [
                RegistryWrite.Delete(DeviceGuardPolicyKey, "EnableVirtualizationBasedSecurity"),
                RegistryWrite.Delete(DeviceGuardPolicyKey, "HVCIMATRequired"),
                RegistryWrite.Delete(DeviceGuardPolicyKey, "LsaCfgFlags"),
                RegistryWrite.Delete(DeviceGuardControlKey, "EnableVirtualizationBasedSecurity")
            ],
            offWrites:
            [
                RegistryWrite.Int(DeviceGuardPolicyKey, "EnableVirtualizationBasedSecurity", 0),
                RegistryWrite.Int(DeviceGuardPolicyKey, "HVCIMATRequired", 0),
                RegistryWrite.Int(DeviceGuardPolicyKey, "LsaCfgFlags", 0),
                RegistryWrite.Int(DeviceGuardControlKey, "EnableVirtualizationBasedSecurity", 0)
            ],
            description: "运行 VMware 虚拟机软件必须禁用它。",
            effect: EffectScope.Reboot);

        yield return new CommandTweak(
            "advanced.bbr2",
            "BBR2 网络拥塞控制算法",
            TweakCategory.Network,
            runner,
            probeCommand: "netsh int tcp show supplemental",
            probeParser: result => result.Contains("bbr2") ? TweakState.On : TweakState.Off,
            onCommand: "chcp 437 >NUL && netsh int tcp set supplemental Template=Internet CongestionProvider=bbr2 && netsh int tcp set supplemental Template=Datacenter CongestionProvider=bbr2 && netsh int tcp set supplemental Template=Compat CongestionProvider=bbr2 && netsh int tcp set supplemental Template=DatacenterCustom CongestionProvider=bbr2 && netsh int tcp set supplemental Template=InternetCustom CongestionProvider=bbr2",
            offCommand: "netsh int tcp set supplemental Template=Internet CongestionProvider=CUBIC && netsh int tcp set supplemental Template=Datacenter CongestionProvider=CUBIC && netsh int tcp set supplemental Template=Compat CongestionProvider=NewReno && netsh int tcp set supplemental Template=DatacenterCustom CongestionProvider=CUBIC && netsh int tcp set supplemental Template=InternetCustom CongestionProvider=CUBIC")
        {
            Description = "开启 BBR2 可提升网络响应速度，要求 Win11 22H2 及以上。注意：Win11 24H2 上开启可能引发问题，如无必要建议保持关闭。",
            Effect = EffectScope.Immediate
        };

        yield return new CommandTweak(
            "advanced.dep",
            "数据执行保护（DEP）",
            TweakCategory.Advanced,
            runner,
            probeCommand: "bcdedit | find \"nx\"",
            probeParser: result => result.Contains("AlwaysOff") ? TweakState.Off : TweakState.On,
            onCommand: "bcdedit.exe /set {current} nx OptIn",
            offCommand: "chcp 437 >NUL && bcdedit.exe /set {current} nx AlwaysOff")
        {
            Description = "关闭 DEP 能小幅提升性能，但会降低系统安全性。若启用了 UEFI 安全启动则无法关闭。",
            Effect = EffectScope.Reboot,
            SuccessMarker = "successfully",
            FailureMessage = "禁止 DEP 失败。如果您使用的是 UEFI 安全启动，DEP 无法被禁止；可尝试在 BIOS 中关闭安全启动后重试。"
        };

        yield return factory.Custom(
            "advanced.uac",
            "用户账户控制（UAC）",
            TweakCategory.Advanced,
            reg => reg.GetInt(PoliciesSystemKey, "ConsentPromptBehaviorAdmin") == 5 ? TweakState.On : TweakState.Off,
            onWrites:
            [
                RegistryWrite.Int(PoliciesSystemKey, "EnableLUA", 1),
                RegistryWrite.Int(PoliciesSystemKey, "EnableVirtualization", 2),
                RegistryWrite.Int(PoliciesSystemKey, "EnableInstallerDetection", 1),
                RegistryWrite.Int(PoliciesSystemKey, "PromptOnSecureDesktop", 2),
                RegistryWrite.Int(PoliciesSystemKey, "EnableSecureUIAPaths", 1),
                RegistryWrite.Int(PoliciesSystemKey, "ConsentPromptBehaviorAdmin", 5),
                RegistryWrite.Int(PoliciesSystemKey, "ConsentPromptBehaviorUser", 3)
            ],
            offWrites:
            [
                RegistryWrite.Int(PoliciesSystemKey, "EnableLUA", 1),
                RegistryWrite.Int(PoliciesSystemKey, "EnableVirtualization", 0),
                RegistryWrite.Int(PoliciesSystemKey, "EnableInstallerDetection", 0),
                RegistryWrite.Int(PoliciesSystemKey, "PromptOnSecureDesktop", 0),
                RegistryWrite.Int(PoliciesSystemKey, "EnableSecureUIAPaths", 0),
                RegistryWrite.Int(PoliciesSystemKey, "ConsentPromptBehaviorAdmin", 0),
                RegistryWrite.Int(PoliciesSystemKey, "ConsentPromptBehaviorUser", 0)
            ],
            description: "关闭后运行需提权的程序不再弹出确认窗口，减少打扰但显著降低安全性。普通用户建议保持开启。");

        yield return new CommandTweak(
            "advanced.reservedStorage",
            "Windows 系统保留的存储",
            TweakCategory.Advanced,
            runner,
            probeCommand: "DISM /English /Online /Get-ReservedStorageState",
            probeParser: result => result.Contains("Reserved storage is enabled.") ? TweakState.On : TweakState.Off,
            onCommand: "DISM.exe /English /Online /Set-ReservedStorageState /State:Enabled",
            offCommand: "DISM.exe /English /Online /Set-ReservedStorageState /State:Disabled")
        {
            Description = "为更新和临时文件预留磁盘空间。磁盘紧张时可关闭。",
            SuccessMarker = "successfully",
            FailureMessage = "设置系统保留存储失败，可能是磁盘空间不足。"
        };
    }
}
