using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Settings;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Catalog;

/// <summary>
/// "系统功能调节" — hibernation, memory management (MMAgent) and remote access.
///
/// The dependency rules are:
///   * 快速启动 is only adjustable while 系统休眠 is on;
///   * 内存压缩 / 应用预启动 / 应用启动预取 / 内存页面合并 / 最大预取文件数 are only
///     adjustable while the SysMain service is running;
///   * 应用启动预取 is read-only (it is reported for information only).
/// </summary>
internal static class PerformanceTweaks
{
    public const string HibernateId = "performance.hibernate";
    public const string SysMainId = "performance.sysMain";

    private const string PowerKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power";
    private const string RemoteAssistanceKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance";
    private const string TerminalServerKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Terminal Server";
    private const string RdpTcpKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp";
    private const string UcpdServiceKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\UCPD";

    public static IEnumerable<ITweak> Create(
        TweakFactory factory,
        ICommandRunner runner,
        IWindowsServiceManager services)
    {
        yield return new HibernationTweak(runner)
        {
            Description = "关闭后可释放与内存等大的 hiberfil.sys 磁盘空间，但将无法使用休眠与快速启动。"
        };

        yield return factory.Value(
            "performance.fastStartup", "快速启动", TweakCategory.Performance,
            PowerKey, "HiberbootEnabled", onValue: 1, offValue: 0, defaultWhenMissing: 1,
            description: "关机时保存内核状态以加快开机。依赖系统休眠功能。",
            effect: EffectScope.Reboot,
            requiresOn: [HibernateId]);

        yield return new ServiceTweak(
            SysMainId, "SysMain 服务（原 SuperFetch）", TweakCategory.Performance,
            services, "SysMain", probeRunningStatus: true)
        {
            Description = "维护和提高一段时间内的系统性能。禁用后下方内存管理相关项将不可调整。"
        };

        yield return MemoryAgentTweak.Create(
            runner, "performance.memoryCompression", "内存压缩", "MemoryCompression", "mc",
            "压缩内存页以减少物理内存占用，代价是少量 CPU 开销。");

        yield return MemoryAgentTweak.Create(
            runner, "performance.appPrelaunch", "应用预启动", "ApplicationPreLaunch", "ApplicationPreLaunch",
            "提前在后台预启动应用以加快打开速度。");

        yield return MemoryAgentTweak.Create(
            runner, "performance.appPrefetch", "应用启动预取（只读）", "ApplicationLaunchPrefetching", null,
            "系统的应用启动预取状态。原程序中此项仅供查看，不可更改。",
            isReadOnly: true);

        yield return MemoryAgentTweak.Create(
            runner, "performance.pageCombining", "内存页面合并", "PageCombining", "PageCombining",
            "合并内容相同的内存页以节省内存。");

        yield return factory.Value(
            "performance.remoteAssistance", "远程协助", TweakCategory.Performance,
            RemoteAssistanceKey, "fAllowToGetHelp", onValue: 1, offValue: 0, defaultWhenMissing: 0,
            description: "允许他人通过“远程协助”连接到本机。不使用时建议关闭。");

        // Inverted semantics: the registry value *denies* connections, so enabling RDP writes 0.
        yield return factory.Custom(
            "performance.remoteDesktop", "远程桌面", TweakCategory.Performance,
            reg => (reg.GetInt(TerminalServerKey, "fDenyTSConnections") ?? 1) >= 1 ? TweakState.Off : TweakState.On,
            [RegistryWrite.Int(TerminalServerKey, "fDenyTSConnections", 0)],
            [RegistryWrite.Int(TerminalServerKey, "fDenyTSConnections", 1)],
            description: "允许通过远程桌面（RDP）连接到本机。");

        yield return factory.Custom(
            "performance.disableUcpd", "禁止微软 UCPD 驱动", TweakCategory.Performance,
            reg => reg.GetInt(UcpdServiceKey, "Start") == 4 ? TweakState.On : TweakState.Off,
            [RegistryWrite.Int(UcpdServiceKey, "Start", 4)],
            [RegistryWrite.Int(UcpdServiceKey, "Start", 2)],
            description: "UCPD 驱动会拦截对部分注册表项的修改。若某些设置总是失败，可禁用它并重启电脑后再试。",
            effect: EffectScope.Reboot);
    }

    public static IEnumerable<INumericSetting> CreateNumeric(ICommandRunner runner, IRegistryService registry)
    {
        yield return new NumericSetting(
            "performance.maxPrefetchFiles", "最大预取文件数", TweakCategory.Performance,
            minimum: 1, maximum: 8192,
            read: async () =>
            {
                var result = await runner.RunPowerShellAsync("Get-MMAgent").ConfigureAwait(false);
                foreach (var line in result.Output.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("MaxOperationAPIFiles", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var separator = trimmed.IndexOf(':');
                    if (separator >= 0 && int.TryParse(trimmed[(separator + 1)..].Trim(), out var parsed))
                    {
                        return parsed;
                    }
                }

                return 512;
            },
            write: async value =>
            {
                var result = await runner.RunPowerShellAsync($"Set-MMAgent -MaxOperationAPIFiles {value}").ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(result.StandardError)
                    ? TweakApplyResult.Ok()
                    : TweakApplyResult.Fail(result.StandardError.Trim());
            })
        {
            Description = "Operation Recorder 可记录的最大预取文件数量。",
            RequiresOn = [SysMainId]
        };

        yield return new NumericSetting(
            "performance.rdpPort", "远程桌面端口", TweakCategory.Performance,
            minimum: 1, maximum: 65535,
            read: () => Task.FromResult(registry.GetInt(RdpTcpKey, "PortNumber") ?? 3389),
            write: async value =>
            {
                try
                {
                    registry.SetInt(RdpTcpKey, "PortNumber", value);
                    await runner.RunPowerShellAsync(
                        "Remove-NetFirewallRule -DisplayName 'RDPPORTLatest-TCP-In' -ErrorAction SilentlyContinue;" +
                        "Remove-NetFirewallRule -DisplayName 'RDPPORTLatest-UDP-In' -ErrorAction SilentlyContinue;" +
                        $"New-NetFirewallRule -DisplayName 'RDPPORTLatest-TCP-In' -Direction Inbound -Action Allow -Protocol TCP -LocalPort {value};" +
                        $"New-NetFirewallRule -DisplayName 'RDPPORTLatest-UDP-In' -Direction Inbound -Action Allow -Protocol UDP -LocalPort {value}")
                        .ConfigureAwait(false);

                    return new TweakApplyResult(true, "端口已更改，重启电脑后生效。");
                }
                catch (Exception ex)
                {
                    return TweakApplyResult.Fail(ex.Message);
                }
            })
        {
            Description = "更改远程桌面监听端口，并同步创建对应的防火墙规则。重启后生效。"
        };
    }

    public static IEnumerable<IActionCommand> CreateActions(ICommandRunner runner)
    {
        yield return new ActionCommand("action.firewallStatus", "防火墙状态…", TweakCategory.Performance, "网络与诊断",
            _ => { runner.Launch("firewall.cpl"); return Task.FromResult(TweakApplyResult.Ok()); });

        yield return new ActionCommand("action.firewallRules", "防火墙规则设置…", TweakCategory.Performance, "网络与诊断",
            _ => { runner.Launch("wf.msc"); return Task.FromResult(TweakApplyResult.Ok()); });

        yield return new ActionCommand("action.flushDns", "刷新 DNS 解析缓存", TweakCategory.Performance, "网络与诊断",
            async ct =>
            {
                await runner.RunCmdAsync("ipconfig /flushdns", ct).ConfigureAwait(false);
                return new TweakApplyResult(true, "DNS 解析缓存已刷新。");
            });

        yield return new ActionCommand("action.editHosts", "修改 HOSTS 文件", TweakCategory.Performance, "网络与诊断",
            _ =>
            {
                var hosts = Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");
                runner.Launch("notepad.exe", $"\"{hosts}\"");
                return Task.FromResult(TweakApplyResult.Ok());
            });

        yield return new ActionCommand("action.eventViewer", "事件查看器…", TweakCategory.Performance, "网络与诊断",
            _ => { runner.Launch("eventvwr.msc"); return Task.FromResult(TweakApplyResult.Ok()); });

        yield return new ActionCommand("action.clearEventLogs", "清除系统日志", TweakCategory.Performance, "网络与诊断",
            async ct =>
            {
                await runner.RunPowerShellAsync(
                    "Get-EventLog -List | ForEach-Object { Clear-EventLog -LogName $_.Log -ErrorAction SilentlyContinue }", ct)
                    .ConfigureAwait(false);
                return new TweakApplyResult(true, "系统日志已清除。");
            })
        {
            ConfirmationMessage = "这将清空 Windows 事件日志中的全部记录，且无法恢复。是否继续？",
            IsDestructive = true
        };
    }
}

/// <summary>
/// Hibernation is probed by the presence of hiberfil.sys and toggled through powercfg.
/// </summary>
internal sealed class HibernationTweak(ICommandRunner runner)
    : TweakBase(PerformanceTweaks.HibernateId, "系统休眠", TweakCategory.Performance)
{
    public override Task<TweakState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (string.IsNullOrEmpty(systemDrive))
        {
            return Task.FromResult(TweakState.Unknown);
        }

        return Task.FromResult(File.Exists(Path.Combine(systemDrive, "hiberfil.sys")) ? TweakState.On : TweakState.Off);
    }

    public override async Task<TweakApplyResult> ApplyAsync(bool enable, CancellationToken cancellationToken = default)
    {
        var result = await runner.RunCmdAsync(enable ? "powercfg /h on 2>&1" : "powercfg /h off", cancellationToken)
            .ConfigureAwait(false);

        if (enable && (result.Contains("not support") || result.Contains("不支持")))
        {
            return TweakApplyResult.Fail("启用休眠失败：您的系统固件不支持系统休眠。");
        }

        return TweakApplyResult.Ok();
    }
}

/// <summary>
/// Windows memory agent (MMAgent) features, all probed through a single <c>Get-MMAgent</c> call
/// and all gated on the SysMain service running.
/// </summary>
internal static class MemoryAgentTweak
{
    public static ITweak Create(
        ICommandRunner runner,
        string id,
        string title,
        string propertyName,
        string? switchName,
        string description,
        bool isReadOnly = false)
    {
        return new PowerShellStateTweak(
            id,
            title,
            TweakCategory.Performance,
            runner,
            probeCommand: "Get-MMAgent",
            probeParser: result =>
            {
                foreach (var line in result.Output.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var separator = trimmed.IndexOf(':');
                    if (separator < 0)
                    {
                        continue;
                    }

                    var value = trimmed[(separator + 1)..].Trim();
                    return value.Equals("True", StringComparison.OrdinalIgnoreCase) ? TweakState.On : TweakState.Off;
                }

                return TweakState.Unknown;
            },
            onCommand: switchName is null ? null : $"Enable-MMAgent -{switchName}",
            offCommand: switchName is null ? null : $"Disable-MMAgent -{switchName}")
        {
            Description = description,
            Effect = EffectScope.Reboot,
            IsReadOnly = isReadOnly,
            RequiresOn = [PerformanceTweaks.SysMainId]
        };
    }
}

/// <summary>Like <see cref="CommandTweak"/> but runs through PowerShell.</summary>
internal sealed class PowerShellStateTweak(
    string id,
    string title,
    TweakCategory category,
    ICommandRunner runner,
    string probeCommand,
    Func<CommandResult, TweakState> probeParser,
    string? onCommand,
    string? offCommand) : TweakBase(id, title, category)
{
    public override async Task<TweakState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await runner.RunPowerShellAsync(probeCommand, cancellationToken).ConfigureAwait(false);
            return probeParser(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return TweakState.Unknown;
        }
    }

    public override async Task<TweakApplyResult> ApplyAsync(bool enable, CancellationToken cancellationToken = default)
    {
        var command = enable ? onCommand : offCommand;
        if (command is null)
        {
            return TweakApplyResult.Fail("此项为只读，无法更改。");
        }

        try
        {
            var result = await runner.RunPowerShellAsync(command, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(result.StandardError)
                ? TweakApplyResult.Ok()
                : TweakApplyResult.Fail(result.StandardError.Trim());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TweakApplyResult.Fail(ex.Message);
        }
    }
}
