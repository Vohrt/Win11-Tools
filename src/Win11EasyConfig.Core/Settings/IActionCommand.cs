using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Settings;

/// <summary>
/// A one-shot operation exposed as a button (快速打开 / 清空回收站 / 刷新DNS / 清除系统日志…).
/// </summary>
public interface IActionCommand
{
    string Id { get; }

    string Title { get; }

    string? Description { get; }

    TweakCategory Category { get; }

    string GroupName { get; }

    /// <summary>Shown as a confirmation prompt before running; null runs immediately.</summary>
    string? ConfirmationMessage { get; }

    bool IsDestructive { get; }

    Task<TweakApplyResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Delegate-backed command.</summary>
public sealed class ActionCommand(
    string id,
    string title,
    TweakCategory category,
    string groupName,
    Func<CancellationToken, Task<TweakApplyResult>> execute) : IActionCommand
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string? Description { get; init; }

    public TweakCategory Category { get; } = category;

    public string GroupName { get; } = groupName;

    public string? ConfirmationMessage { get; init; }

    public bool IsDestructive { get; init; }

    public Task<TweakApplyResult> ExecuteAsync(CancellationToken cancellationToken = default) => execute(cancellationToken);
}

/// <summary>
/// A numeric setting rendered as a spin box plus an apply button
/// (最大预取文件数 / 远程桌面端口).
/// </summary>
public interface INumericSetting
{
    string Id { get; }

    string Title { get; }

    string? Description { get; }

    TweakCategory Category { get; }

    int Minimum { get; }

    int Maximum { get; }

    IReadOnlyList<string> RequiresOn { get; }

    Task<int> GetValueAsync(CancellationToken cancellationToken = default);

    Task<TweakApplyResult> ApplyAsync(int value, CancellationToken cancellationToken = default);
}

public sealed class NumericSetting(
    string id,
    string title,
    TweakCategory category,
    int minimum,
    int maximum,
    Func<Task<int>> read,
    Func<int, Task<TweakApplyResult>> write) : INumericSetting
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string? Description { get; init; }

    public TweakCategory Category { get; } = category;

    public int Minimum { get; } = minimum;

    public int Maximum { get; } = maximum;

    public IReadOnlyList<string> RequiresOn { get; init; } = [];

    public Task<int> GetValueAsync(CancellationToken cancellationToken = default) => read();

    public Task<TweakApplyResult> ApplyAsync(int value, CancellationToken cancellationToken = default) => write(value);
}
