using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Services;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Catalog;

/// <summary>
/// "隐私设置" — telemetry, search and personalisation switches.
/// Each toggle is phrased so that "on" means the privacy-friendly choice.
/// </summary>
internal static class PrivacyTweaks
{
    private const string SearchPolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search";
    private const string SearchKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search";
    private const string SearchSettingsKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings";
    private const string AdvertisingKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo";
    private const string UserProfileKey = @"HKEY_CURRENT_USER\Control Panel\International\User Profile";
    private const string AdvancedKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ContentDeliveryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string DeliveryOptimizationKey = @"HKEY_USERS\S-1-5-20\Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Settings";
    private const string WindowsUpdatePolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string MrtPolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\MRT";
    private const string PersonalizationKey = @"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings";

    public static IEnumerable<ITweak> Create(TweakFactory factory, WindowsInfoService windowsInfo)
    {
        yield return factory.Custom(
            "privacy.cloudSearch", "搜索界面禁止云内容搜索", TweakCategory.Privacy,
            reg => reg.GetInt(SearchPolicyKey, "AllowCloudSearch") == 0 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Int(SearchPolicyKey, "AllowCloudSearch", 0)],
            [RegistryWrite.Delete(SearchPolicyKey, "AllowCloudSearch")],
            description: "云搜索内容来源包括 OneDrive、SharePoint、Outlook、必应等。");

        yield return factory.Custom(
            "privacy.webSearch", "搜索界面禁止 Web 搜索（当前用户）", TweakCategory.Privacy,
            reg => reg.GetInt(SearchKey, "BingSearchEnabled") == 0 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Int(SearchKey, "BingSearchEnabled", 0)],
            [RegistryWrite.Delete(SearchKey, "BingSearchEnabled")],
            description: "仅从界面隐藏 Web 搜索结果。若要彻底阻止搜索内容上传，请另外添加防火墙规则。");

        yield return factory.Custom(
            "privacy.searchHistory", "禁止本地存储搜索历史记录", TweakCategory.Privacy,
            reg => reg.GetInt(SearchSettingsKey, "IsDeviceSearchHistoryEnabled") == 0 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Int(SearchSettingsKey, "IsDeviceSearchHistoryEnabled", 0)],
            [RegistryWrite.Delete(SearchSettingsKey, "IsDeviceSearchHistoryEnabled")]);

        yield return factory.Value(
            "privacy.advertisingId", "允许应用使用广告 ID 展示个性化广告", TweakCategory.Privacy,
            AdvertisingKey, "Enabled", onValue: 1, offValue: 0, defaultWhenMissing: 1,
            description: "建议关闭。");

        yield return factory.Custom(
            "privacy.languageList", "允许网站通过访问语言列表显示本地相关内容", TweakCategory.Privacy,
            reg => (reg.GetInt(UserProfileKey, "HttpAcceptLanguageOptOut") ?? -1) <= 0 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Delete(UserProfileKey, "HttpAcceptLanguageOptOut")],
            [RegistryWrite.Int(UserProfileKey, "HttpAcceptLanguageOptOut", 1)],
            description: "建议关闭。");

        yield return factory.Value(
            "privacy.trackAppLaunch", "允许 Windows 跟踪应用启动以改进搜索结果", TweakCategory.Privacy,
            AdvancedKey, "Start_TrackProgs", onValue: 1, offValue: 0, defaultWhenMissing: 1,
            description: "建议关闭。");

        yield return factory.Custom(
            "privacy.settingsSuggestions", "在设置应用中显示建议的内容", TweakCategory.Privacy,
            reg =>
            {
                var a = reg.GetInt(ContentDeliveryKey, "SubscribedContent-338393Enabled") ?? 0;
                var b = reg.GetInt(ContentDeliveryKey, "SubscribedContent-353694Enabled") ?? 0;
                var c = reg.GetInt(ContentDeliveryKey, "SubscribedContent-353696Enabled") ?? 0;
                return a != 0 || b != 0 || c != 0 ? TweakState.On : TweakState.Off;
            },
            onWrites:
            [
                RegistryWrite.Int(ContentDeliveryKey, "SubscribedContent-338393Enabled", 1),
                RegistryWrite.Int(ContentDeliveryKey, "SubscribedContent-353694Enabled", 1),
                RegistryWrite.Int(ContentDeliveryKey, "SubscribedContent-353696Enabled", 1)
            ],
            offWrites:
            [
                RegistryWrite.Int(ContentDeliveryKey, "SubscribedContent-338393Enabled", 0),
                RegistryWrite.Int(ContentDeliveryKey, "SubscribedContent-353694Enabled", 0),
                RegistryWrite.Int(ContentDeliveryKey, "SubscribedContent-353696Enabled", 0)
            ],
            description: "建议关闭。");

        yield return factory.Custom(
            "privacy.deliveryOptimization", "禁止 Windows 更新传递优化", TweakCategory.Privacy,
            reg => reg.GetInt(DeliveryOptimizationKey, "DownloadMode") == 0 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Int(DeliveryOptimizationKey, "DownloadMode", 0)],
            [RegistryWrite.Int(DeliveryOptimizationKey, "DownloadMode", 1)],
            description: "关闭后本机不再作为更新分发的上传源，可节省上行带宽。");

        var displayVersion = windowsInfo.GetVersion().DisplayVersion;
        yield return factory.Custom(
            "privacy.blockFeatureUpdate", $"禁止 Windows 大版本更新（锁定在 {displayVersion}）", TweakCategory.Privacy,
            reg => reg.GetInt(WindowsUpdatePolicyKey, "TargetReleaseVersion") == 1
                   && reg.GetString(WindowsUpdatePolicyKey, "TargetReleaseVersionInfo") == displayVersion
                ? TweakState.On
                : TweakState.Off,
            onWrites:
            [
                RegistryWrite.Int(WindowsUpdatePolicyKey, "TargetReleaseVersion", 1),
                RegistryWrite.String(WindowsUpdatePolicyKey, "TargetReleaseVersionInfo", displayVersion)
            ],
            offWrites:
            [
                RegistryWrite.Delete(WindowsUpdatePolicyKey, "TargetReleaseVersion"),
                RegistryWrite.Delete(WindowsUpdatePolicyKey, "TargetReleaseVersionInfo")
            ],
            description: "把功能更新锁定在当前版本，例如阻止 23H2 自动升级到 24H2。");

        yield return factory.Custom(
            "privacy.excludeMrt", "Windows 更新不包括恶意软件删除工具", TweakCategory.Privacy,
            reg => (reg.GetInt(MrtPolicyKey, "DontOfferThroughWUAU") ?? 0) > 0 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Int(MrtPolicyKey, "DontOfferThroughWUAU", 1)],
            [RegistryWrite.Delete(MrtPolicyKey, "DontOfferThroughWUAU")]);

        yield return factory.Custom(
            "privacy.inkingDictionary", "自定义墨迹书写和键入词典", TweakCategory.Privacy,
            reg => (reg.GetInt(PersonalizationKey, "AcceptedPrivacyPolicy") ?? 0) > 0 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Int(PersonalizationKey, "AcceptedPrivacyPolicy", 1)],
            [RegistryWrite.Int(PersonalizationKey, "AcceptedPrivacyPolicy", 0)],
            description: "建议关闭，避免输入内容被用于个性化词典。");
    }
}
