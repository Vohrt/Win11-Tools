using System.Runtime.Versioning;
using Win11EasyConfig.Core.Abstractions;

namespace Win11EasyConfig.Core.Features;

public sealed record EdgeComponentState(string Name, string? Version, bool Installed, bool IsEnterprise = false)
{
    public string DisplayStatus => !Installed
        ? "未安装"
        : IsEnterprise ? $"{Version}（企业版）" : $"已安装版本：{Version}";
}

public sealed record EdgeOverview(
    EdgeComponentState Stable,
    EdgeComponentState Beta,
    EdgeComponentState Dev,
    EdgeComponentState Canary,
    EdgeComponentState GlobalWebView2,
    EdgeComponentState UserWebView2,
    bool StartupBoostDisabled);

public enum EdgeChannel { Stable, Beta, Dev, Canary }

/// <summary>
/// Edge and WebView2 management. Install/upgrade downloads the official installers;
/// uninstall performs a forced cleanup of every channel and the WebView2 runtime.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EdgeService(IRegistryService registry, ICommandRunner runner)
{
    private const string Wow6432Uninstall = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string EdgePolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge";

    // The go.microsoft.com fwlink variants resolve to the enterprise ".pkg" package
    // (a ~267 MB XAR archive, not an executable), so running the downloaded file as an
    // installer would always fail. The c2rsetup endpoints below serve the real installer.
    private static readonly IReadOnlyDictionary<EdgeChannel, string> InstallUrls = new Dictionary<EdgeChannel, string>
    {
        [EdgeChannel.Stable] = "https://c2rsetup.officeapps.live.com/c2r/downloadEdge.aspx?platform=Default&source=EdgeStablePage&Channel=Stable&language=zh-cn&brand=M100",
        [EdgeChannel.Beta] = "https://c2rsetup.edog.officeapps.live.com/c2r/downloadEdge.aspx?platform=Default&source=EdgeInsiderPage&Channel=Beta&language=zh-cn",
        [EdgeChannel.Dev] = "https://c2rsetup.edog.officeapps.live.com/c2r/downloadEdge.aspx?platform=Default&source=EdgeInsiderPage&Channel=Dev&language=zh-cn",
        [EdgeChannel.Canary] = "https://c2rsetup.edog.officeapps.live.com/c2r/downloadEdge.aspx?platform=Default&source=EdgeInsiderPage&Channel=Canary&language=zh-cn"
    };

    private const string WebView2Url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public EdgeOverview GetOverview()
    {
        return new EdgeOverview(
            Stable: DetectStable(),
            Beta: DetectChannel("Beta", @"Microsoft Edge Beta", "{EE5F1C18-EF37-3048-894A-2F363967691D}"),
            Dev: DetectChannel("Dev", @"Microsoft Edge Dev", "{D2B0A5B7-2117-36FF-8FBB-95978645C45C}"),
            Canary: DetectCanary(),
            GlobalWebView2: DetectVersion("全局 WebView2",
                $@"{Wow6432Uninstall}\Microsoft EdgeWebView", "Version"),
            UserWebView2: DetectVersion("当前用户 WebView2",
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView", "Version"),
            StartupBoostDisabled: registry.GetInt(EdgePolicyKey, "StartupBoostEnabled") == 0);
    }

    public async Task InstallEdgeAsync(EdgeChannel channel, CancellationToken cancellationToken = default)
    {
        // Download file name per channel (Canary is capitalized).
        var fileName = "MicrosoftEdgeSetup_" + channel switch
        {
            EdgeChannel.Stable => "stable",
            EdgeChannel.Beta => "beta",
            EdgeChannel.Dev => "dev",
            _ => "Canary"
        } + ".exe";
        await DownloadAndRunAsync(InstallUrls[channel], fileName, cancellationToken).ConfigureAwait(false);
    }

    public Task InstallWebView2Async(CancellationToken cancellationToken = default) =>
        DownloadAndRunAsync(WebView2Url, "MicrosoftEdgeWebview2Setup.exe", cancellationToken);

    /// <summary>Removes the Edge/WebView2 update services and scheduled tasks (禁止自动更新).</summary>
    public async Task DisableAutoUpdateAsync(CancellationToken cancellationToken = default)
    {
        foreach (var command in new[]
        {
            "taskkill /f /im MicrosoftEdgeUpdate.exe",
            "sc delete MicrosoftEdgeElevationService",
            "sc delete edgeupdate",
            "sc delete edgeupdatem",
            "rd /S /Q \"%ProgramFiles(x86)%\\Microsoft\\EdgeUpdate\"",
            "rd /S /Q \"%localappdata%\\Microsoft\\EdgeUpdate\"",
            "rd /S /Q \"%ProgramFiles(x86)%\\Microsoft\\EdgeCore\"",
            "rd /S /Q \"%localappdata%\\Microsoft\\EdgeCore\""
        })
        {
            await runner.RunCmdAsync(command, cancellationToken).ConfigureAwait(false);
        }

        await DeleteEdgeTasksAsync(cancellationToken).ConfigureAwait(false);

        registry.DeleteKeyTree(@"HKEY_CURRENT_USER\Software\Microsoft\EdgeUpdate");
        registry.DeleteKeyTree($@"{Wow6432Uninstall}\Microsoft Edge Update");
        registry.DeleteValue($@"{Wow6432Uninstall}\Microsoft EdgeWebView", "ModifyPath");
        registry.DeleteValue($@"{Wow6432Uninstall}\Microsoft Edge", "ModifyPath");
        registry.DeleteValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "Microsoft Edge Update");
    }

    /// <summary>Forcibly uninstalls the global WebView2 runtime.</summary>
    public async Task<bool> UninstallGlobalWebView2Async(CancellationToken cancellationToken = default)
    {
        const string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView";
        var uninstallString = registry.GetString(key, "UninstallString");
        if (uninstallString is null)
        {
            return false;
        }

        await runner.RunCmdAsync("taskkill /f /im Widgets.exe", cancellationToken).ConfigureAwait(false);
        await runner.RunCmdAsync("taskkill /f /im WidgetService.exe", cancellationToken).ConfigureAwait(false);
        await runner.RunCmdAsync("taskkill /f /im msedgewebview2.exe", cancellationToken).ConfigureAwait(false);
        await runner.RunCmdAsync($"{uninstallString} --force-uninstall", cancellationToken).ConfigureAwait(false);

        var installLocation = registry.GetString(key, "InstallLocation");
        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            await runner.RunCmdAsync($"rd /S /Q \"{installLocation}\"", cancellationToken).ConfigureAwait(false);
        }

        registry.DeleteKeyTree(key);
        registry.DeleteKeyTree(@"HKEY_CURRENT_USER\Software\Microsoft\EdgeWebView");
        return true;
    }

    public void SetStartupBoostDisabled(bool disabled)
    {
        if (disabled)
        {
            registry.SetInt(EdgePolicyKey, "StartupBoostEnabled", 0);
        }
        else
        {
            registry.DeleteValue(EdgePolicyKey, "StartupBoostEnabled");
        }
    }

    private async Task DeleteEdgeTasksAsync(CancellationToken cancellationToken)
    {
        var result = await runner.RunCmdAsync(
            "schtasks /query /fo csv | findstr /I MicrosoftEdge", cancellationToken).ConfigureAwait(false);

        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line.Split(',')[0].Trim().Trim('"');
            if (name.Contains("MicrosoftEdge", StringComparison.OrdinalIgnoreCase))
            {
                await runner.RunCmdAsync($"SCHTASKS /Delete /F /TN \"{name}\"", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task DownloadAndRunAsync(string url, string fileName, CancellationToken cancellationToken)
    {
        // Download to %temp% with iwr, then run the installer and wait for it to finish.
        var script =
            $"$ProgressPreference='SilentlyContinue';" +
            $"cd \"$env:temp\";" +
            $"Invoke-WebRequest -Uri '{url}' -OutFile '{fileName}';" +
            $"Start-Process -Wait '.\\{fileName}'";
        await runner.RunPowerShellAsync(script, cancellationToken).ConfigureAwait(false);
    }

    private EdgeComponentState DetectStable()
    {
        var version = registry.GetString($@"{Wow6432Uninstall}\Microsoft Edge", "Version");
        if (version is not null)
        {
            return new EdgeComponentState("正式版（Stable）", version, true);
        }

        foreach (var (key, name) in new (string, string)[]
        {
            ($@"{Wow6432Uninstall}\{{224D921D-4786-3747-81C8-3C2A7897068A}}", "DisplayVersion"),
            (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\D129D42268747473188CC3A2877960A8\InstallProperties", "DisplayVersion"),
            (@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}", "pv")
        })
        {
            var enterprise = registry.GetString(key, name);
            if (enterprise is not null)
            {
                return new EdgeComponentState("正式版（Stable）", enterprise, true, IsEnterprise: true);
            }
        }

        return new EdgeComponentState("正式版（Stable）", null, false);
    }

    private EdgeComponentState DetectChannel(string label, string uninstallName, string enterpriseGuid)
    {
        var display = label switch
        {
            "Beta" => "测试版（Beta）",
            "Dev" => "开发版（Dev）",
            _ => label
        };

        var version = registry.GetString($@"{Wow6432Uninstall}\{uninstallName}", "Version");
        if (version is not null)
        {
            return new EdgeComponentState(display, version, true);
        }

        var enterprise = registry.GetString($@"{Wow6432Uninstall}\{enterpriseGuid}", "DisplayVersion");
        return enterprise is not null
            ? new EdgeComponentState(display, enterprise, true, IsEnterprise: true)
            : new EdgeComponentState(display, null, false);
    }

    private EdgeComponentState DetectCanary()
    {
        var version = registry.GetString(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge SxS", "Version");
        return new EdgeComponentState("金丝雀版（Canary）", version, version is not null);
    }

    private EdgeComponentState DetectVersion(string label, string key, string valueName)
    {
        var version = registry.GetString(key, valueName);
        return new EdgeComponentState(label, version, version is not null);
    }
}
