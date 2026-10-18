using System.Runtime.Versioning;
using Win11EasyConfig.Core.Abstractions;

namespace Win11EasyConfig.Core.Features;

/// <summary>
/// A Microsoft Store app the tool can install or remove. <see cref="PackagePrefix"/> is the
/// package full-name prefix used to detect installation and to remove the app.
/// </summary>
public sealed record StoreApp(string Name, string StoreId, string PackagePrefix, string? Description = null)
{
    /// <summary>
    /// False for apps without a Store product id (e.g. "Xbox游戏语音窗口"): they can be detected
    /// and removed, but there is no online install entry point.
    /// </summary>
    public bool CanInstallFromStore => !string.IsNullOrEmpty(StoreId);
}

/// <summary>
/// The two management scopes: manage the app for the current user, or remove the
/// provisioned (preinstalled-for-every-new-user) package.
/// </summary>
public enum AppScope
{
    CurrentUser,
    System
}

public enum AppInstallState
{
    Unknown,
    Installed,
    NotInstalled
}

/// <summary>
/// Microsoft Store app management. In "current user" scope it
/// installs (Store / WinGet) and removes apps for the signed-in user; in "system" scope it removes
/// the provisioned package so new users no longer receive it. Detection is done once per refresh
/// (one AppX query for the user, the AppxAllUserStore registry for provisioned packages) and then
/// matched locally, which is far faster than probing each app individually.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AppsService(ICommandRunner runner, IRegistryService registry)
{
    private const string ProvisionedKey =
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications";

    /// <summary>
    /// The full app list (61 entries, in display order; Store ids and package prefixes
    /// verified against Microsoft Store product pages). Apps without a Store id are
    /// detect/uninstall only. All installs go through the standard Store/WinGet entry
    /// points — never route downloads through a third-party mirror.
    /// </summary>
    public static IReadOnlyList<StoreApp> Catalog { get; } =
    [
        new("照片", "9WZDNCRFJBH4", "Microsoft.Windows.Photos_"),
        new("计算器", "9WZDNCRFHVN5", "Microsoft.WindowsCalculator_"),
        new("时钟", "9WZDNCRFJ3PR", "Microsoft.WindowsAlarms_"),
        new("录音机", "9WZDNCRFHWKN", "Microsoft.WindowsSoundRecorder_"),
        new("记事本", "9MSMLRH6LZF3", "Microsoft.WindowsNotepad_"),
        new("画图", "9PCFS5B6T72H", "Microsoft.Paint_"),
        new("天气", "9WZDNCRFJ3Q2", "Microsoft.BingWeather_"),
        new("截图工具", "9MZ95KL8MR0L", "Microsoft.ScreenSketch_"),
        new("相机", "9WZDNCRFJBBG", "Microsoft.WindowsCamera_"),
        new("Cortana", "9NFFX4SZZ23L", "Microsoft.549981C3F5F10_", "微软的个人智能助理（微软小娜）。"),
        new("终端", "9N0DX20HK701", "Microsoft.WindowsTerminal_"),
        new("媒体播放器", "9WZDNCRFJ3PT", "Microsoft.ZuneMusic_"),
        new("电影和电视", "9WZDNCRFJ3P2", "Microsoft.ZuneVideo_"),
        new("资讯", "9WZDNCRFHVFW", "Microsoft.BingNews_"),
        new("DV+HDR扩展", "9PLTG1LWPHLF", "DolbyLaboratories.DolbyVisionAccess_", "看 HDR 和杜比视界视频必备。"),
        new("AV1视频扩展", "9MVZQVXJBQ9V", "Microsoft.AV1VideoExtension_"),
        new("VP9视频扩展", "9N4D0MSMP0PT", "Microsoft.VP9VideoExtensions_"),
        new("Webp图像扩展", "9PG2DK419DRG", "Microsoft.WebpImageExtension_"),
        new("HEIF图像扩展", "9PMMSR1CGPWG", "Microsoft.HEIFImageExtension_"),
        new("原始图像扩展", "9NCTDW2W1BH8", "Microsoft.RawImageExtension_"),
        new("Web媒体扩展", "9N5TDP8VCMHS", "Microsoft.WebMediaExtensions_"),
        new("邮件和日历", "9WZDNCRFHVQM", "microsoft.windowscommunicationsapps_"),
        new("Xbox", "9MV0B5HZVK9Z", "Microsoft.GamingApp_", "全新 Xbox 应用，游玩您喜爱的游戏。"),
        new("Xbox身份验证", "9WZDNCRD1HKW", "Microsoft.XboxIdentityProvider_", "允许 PC 游戏连接到 Xbox Live。不玩 Xbox 可卸载。"),
        new("游戏服务", "9MWPM2CQNLHN", "Microsoft.GamingServices_", "运行 Xbox 游戏必备。不玩可卸载。"),
        new("Xbox主机小帮手", "9WZDNCRFJBD8", "Microsoft.XboxApp_", "把 Xbox One 和 Windows 设备中的好友、游戏和成就整合在一起。"),
        new("Xbox游戏工具栏", "9NZKPSTSNW4P", "Microsoft.XboxGamingOverlay_", "内置于 Windows 的可自定义游戏覆盖工具，可用于屏幕捕获、共享以及与 Xbox 好友聊天。"),
        new("Xbox工具栏插件", "9NBLGGH537C2", "Microsoft.XboxGameOverlay_", "Xbox 游戏工具栏的扩展插件。不使用 Xbox 可将其卸载。"),
        new("小组件", "9MSSGKG348SP", "MicrosoftWindows.Client.WebExperience_"),
        new("地图", "9WZDNCRDTBVB", "Microsoft.WindowsMaps_"),
        new("Clipchamp", "9P1J8S7CCWWT", "Clipchamp.Clipchamp_", "微软自家的视频创建、剪辑软件。"),
        new("使用技巧", "9WZDNCRDTBJJ", "Microsoft.Getstarted_"),
        new("便笺", "9NBLGGH4QGHW", "Microsoft.MicrosoftStickyNotes_"),
        new("Xbox TCUI", "9NKNC0LD5NN6", "Microsoft.Xbox.TCUI_", "供游戏和应用使用 Xbox Live 云功能的 API 集。不使用 Xbox 可将其卸载。"),
        new("Xbox游戏语音窗口", "", "Microsoft.XboxSpeechToTextOverlay_", "用于游戏中语音聊天。此应用无商店产品 ID，仅支持检测与卸载。"),
        new("画图 3D", "9NBLGGH5FV99", "Microsoft.MSPaint_"),
        new("待办ToDo", "9NBLGGH5R558", "Microsoft.Todos_"),
        new("3D 查看器", "9NBLGGH42THS", "Microsoft.Microsoft3DViewer_"),
        new("反馈中心", "9NBLGGH4R32N", "Microsoft.WindowsFeedbackHub_"),
        new("获取帮助", "9PKDZBMV1H3T", "Microsoft.GetHelp_"),
        new("扫描", "9WZDNCRFJ3PV", "Microsoft.WindowsScan_", "便于你扫描文档和图片，并将它们保存到任意位置。"),
        new("快速助手", "9P7BP5VNWKX5", "MicrosoftCorporationII.QuickAssist_", "使用远程连接帮助家人和朋友解决电脑问题，或从他们那里获取帮助。"),
        new("来自设备制造商的HEVC视频扩展", "9N4WGH0Z6VHQ", "Microsoft.HEVCVideoExtension_"),
        new("Power Automate", "9NFTCH6J7FHV", "Microsoft.PowerAutomateDesktop_", "通过模仿用户界面操作，在 Windows 桌面上实现 Web 和桌面应用程序自动化。"),
        new("Solitaire游戏合集", "9WZDNCRFHWD2", "Microsoft.MicrosoftSolitaireCollection_", "包含 Klondike、Spider、FreeCell、TriPeaks 和 Pyramid 等经典纸牌游戏。"),
        new("照片(旧版)", "9NV2L4XVMCXM", "Microsoft.PhotosLegacy_"),
        new("手机连接", "9NMPJ99VJBWV", "Microsoft.YourPhone_", "直接从电脑即时访问手机上的内容。"),
        new("微软365", "9WZDNCRD29V9", "Microsoft.MicrosoftOfficeHub_", "最喜爱的生产力应用和内容的主页。使用 Microsoft 帐户登录即可开始使用免费桌面应用。"),
        new("家庭安全", "9PDJDJS743XF", "MicrosoftCorporationII.MicrosoftFamily_", "帮助家庭培养健康习惯，保护家人的数字与物理安全。"),
        new("Microsoft 人脉", "9NBLGGH10PG8", "Microsoft.People_", "将好友、家人、同事和熟人的联系方式集中到一个地方。"),
        new("Microsoft Teams", "XP8BT8DW290MPQ", "MicrosoftTeams_", "集聊天、视频会议、文件存储、Office 365 等功能于一身的通信和协作软件。"),
        new("Skype", "9WZDNCRFJ364", "Microsoft.SkypeApp_", "Skype 让世界保持沟通。"),
        new("Outlook", "9NRX63209R7B", "Microsoft.OutlookForWindows_", "既熟悉又充满现代感的 Outlook，可将电子邮件、日历和联系人集合在一起。"),
        new("Dev Home (开发人员主页)", "9N8MHTPHNGVV", "Microsoft.Windows.DevHome_", "跟踪所有工作流和编码任务的控制中心，具有简化的设置工具和可自定义仪表板。"),
        new("Microsoft Store", "9WZDNCRFJBMP", "Microsoft.WindowsStore_", "微软商店。可通过 WinGet 或商店页面安装。"),
        new("HEVC 视频扩展", "9NMZLZ57R3T7", "Microsoft.HEVCVideoExtensions_", "付费版的 HEVC 视频扩展（商店售价 7 元）。可与“来自设备制造商的HEVC视频扩展”共存，建议只保留一个。"),
        // Match the PowerToys family root rather than a component package name, so the
        // detection covers the main app family.
        new("PowerToys", "XP89DCGQ3K6VLD", "Microsoft.PowerToys", "微软出品的实用小工具合集，可帮助高级用户调整和简化 Windows 体验。"),
        new("Bandizip MSE", "9P2W3W81SPPB", "Bandisoft.com.15700C60EE320_", "强大的多功能压缩文件管理工具，微软商店版本免费无广告。"),
        new("NanaZip预览版", "9NZL0LRP1BNL", "40174MouriNaruto.NanaZipPreview_", "免费的压缩解压缩软件，支持 ZIP、7Z、RAR 等常用格式，支持 Win11 现代右键菜单。"),
        new("测网速Speedtest", "9NBLGGH4Z1JC", "Ookla.SpeedtestbyOokla_", "Ookla Speedtest 精准的网速测试应用。"),
        new("任务栏透明 TranslucentTB", "9PF4KZ2VN4W9", "28017CharlesMilette.TranslucentTB_", "几乎不占用 CPU 的轻量小工具，可使 Windows 10/11 任务栏半透明或透明。")
    ];

    /// <summary>All package full names installed for the current user (one AppX query).</summary>
    public async Task<IReadOnlyList<string>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        var result = await runner
            .RunPowerShellAsync("Get-AppxPackage | ForEach-Object { $_.PackageFullName }", cancellationToken)
            .ConfigureAwait(false);

        return SplitLines(result.StandardOutput);
    }

    /// <summary>All provisioned package full names (read from the registry, works unelevated).</summary>
    public IReadOnlyList<string> GetProvisionedPackages() => registry.GetSubKeyNames(ProvisionedKey);

    public static bool IsPresent(StoreApp app, IEnumerable<string> packageFullNames) =>
        packageFullNames.Any(name => name.StartsWith(app.PackagePrefix, StringComparison.OrdinalIgnoreCase));

    public string? FindProvisionedName(StoreApp app) =>
        GetProvisionedPackages().FirstOrDefault(name =>
            name.StartsWith(app.PackagePrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Installs the app for the current user via the Store protocol or WinGet.</summary>
    public void Install(StoreApp app, bool useWinGet)
    {
        if (useWinGet)
        {
            runner.Launch("winget", $"install {app.StoreId} -e --accept-source-agreements --accept-package-agreements");
        }
        else
        {
            runner.Launch($"ms-windows-store://pdp?productid={app.StoreId}&mode=mini");
        }
    }

    /// <summary>
    /// Removes the app for the current user. When <paramref name="alsoRemoveProvisioned"/> is set
    /// it also strips the provisioned package (the "同时卸载相应预装应用" option).
    /// </summary>
    public async Task UninstallForUserAsync(
        StoreApp app,
        bool alsoRemoveProvisioned,
        CancellationToken cancellationToken = default)
    {
        var family = app.PackagePrefix.TrimEnd('_');
        await runner.RunPowerShellAsync(
            $"Get-AppxPackage -Name '{family}*' | Remove-AppxPackage -ErrorAction SilentlyContinue", cancellationToken)
            .ConfigureAwait(false);

        if (alsoRemoveProvisioned)
        {
            await RemoveProvisionedAsync(app, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes the provisioned package so new users no longer receive the app
    /// (DISM /Remove-ProvisionedAppxPackage). Returns false when the app is not provisioned.
    /// </summary>
    public async Task<bool> RemoveProvisionedAsync(StoreApp app, CancellationToken cancellationToken = default)
    {
        var packageName = FindProvisionedName(app);
        if (packageName is null)
        {
            return false;
        }

        await runner.RunCmdAsync(
            $"DISM.exe /Online /Remove-ProvisionedAppxPackage /PackageName:{packageName}", cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static IReadOnlyList<string> SplitLines(string output) =>
        output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
