using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Settings;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Catalog;

/// <summary>Privacy action buttons (Windows Search firewall rules and the search service).</summary>
internal static class PrivacyExtras
{
    private const string FirewallRuleName = "阻止Windows搜索联网";
    private const string SearchHostPath =
        @"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe";

    public static IEnumerable<IActionCommand> CreateActions(ICommandRunner runner)
    {
        yield return new ActionCommand("action.addSearchFirewallRule", "添加防火墙规则", TweakCategory.Privacy, "Windows 搜索",
            async ct =>
            {
                await runner.RunPowerShellAsync(
                    $"Remove-NetFirewallRule -DisplayName '{FirewallRuleName}' -ErrorAction SilentlyContinue;" +
                    $"New-NetFirewallRule -DisplayName '{FirewallRuleName}' -Direction Outbound " +
                    $"-Program '{SearchHostPath}' -Action Block", ct).ConfigureAwait(false);
                return new TweakApplyResult(true, "已添加防火墙规则。请确保防火墙处于开启状态才会生效。");
            })
        {
            Description = "阻止开始菜单搜索联网，杜绝搜索输入内容被上传。不影响 Edge 浏览器的搜索。"
        };

        yield return new ActionCommand("action.removeSearchFirewallRule", "移除防火墙规则", TweakCategory.Privacy, "Windows 搜索",
            async ct =>
            {
                await runner.RunPowerShellAsync(
                    $"Remove-NetFirewallRule -DisplayName '{FirewallRuleName}' -ErrorAction SilentlyContinue", ct)
                    .ConfigureAwait(false);
                return new TweakApplyResult(true, "已移除防火墙规则。");
            });

        yield return new ActionCommand("action.disableSearchService", "停止并禁止 Windows Search 服务", TweakCategory.Privacy, "Windows 搜索",
            async ct =>
            {
                await runner.RunCmdAsync("sc stop WSearch & sc config WSearch start= disabled", ct).ConfigureAwait(false);
                return new TweakApplyResult(true, "已停止并禁止 Windows Search 索引服务。");
            })
        {
            Description = "搜索索引会实时扫描文件变化，占用较多资源。可用 Everything 等软件替代。",
            ConfirmationMessage = "这将停止并禁止 Windows Search 索引服务，系统搜索功能会受影响。是否继续？",
            IsDestructive = true
        };

        yield return new ActionCommand("action.enableSearchService", "恢复并允许 Windows Search 服务", TweakCategory.Privacy, "Windows 搜索",
            async ct =>
            {
                await runner.RunCmdAsync("sc config WSearch start= delayed-auto & sc start WSearch", ct).ConfigureAwait(false);
                return new TweakApplyResult(true, "已恢复 Windows Search 索引服务。");
            });
    }
}
