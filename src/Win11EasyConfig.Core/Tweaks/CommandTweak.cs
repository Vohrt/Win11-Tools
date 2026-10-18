using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;

namespace Win11EasyConfig.Core.Tweaks;

/// <summary>
/// A tweak driven by console commands: one probe command whose output decides the state, and an
/// on/off command pair. <see cref="SuccessMarker"/> lets the catalog verify the command actually
/// worked (DISM/netsh/bcdedit report success in their output rather than the exit code).
/// </summary>
public sealed class CommandTweak : TweakBase
{
    private readonly ICommandRunner _runner;
    private readonly string _probeCommand;
    private readonly Func<CommandResult, TweakState> _probeParser;
    private readonly string _onCommand;
    private readonly string _offCommand;

    public CommandTweak(
        string id,
        string title,
        TweakCategory category,
        ICommandRunner runner,
        string probeCommand,
        Func<CommandResult, TweakState> probeParser,
        string onCommand,
        string offCommand)
        : base(id, title, category)
    {
        _runner = runner;
        _probeCommand = probeCommand;
        _probeParser = probeParser;
        _onCommand = onCommand;
        _offCommand = offCommand;
    }

    /// <summary>Text that must appear in the output for an apply to count as successful.</summary>
    public string? SuccessMarker { get; init; }

    /// <summary>Message surfaced when <see cref="SuccessMarker"/> is missing from the output.</summary>
    public string? FailureMessage { get; init; }

    public override async Task<TweakState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runner.RunCmdAsync(_probeCommand, cancellationToken).ConfigureAwait(false);
            return _probeParser(result);
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
        try
        {
            var result = await _runner.RunCmdAsync(enable ? _onCommand : _offCommand, cancellationToken).ConfigureAwait(false);

            if (SuccessMarker is not null && !result.Contains(SuccessMarker))
            {
                return TweakApplyResult.Fail(FailureMessage ?? "命令执行未返回成功标志。");
            }

            return TweakApplyResult.Ok();
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
