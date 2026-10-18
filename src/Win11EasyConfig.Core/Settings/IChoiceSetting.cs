using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Settings;

public sealed record ChoiceOption(string Label, int Value);

/// <summary>
/// A setting with more than two states, rendered as a combo box (搜索按钮 / 任务栏对齐 /
/// 任务栏按钮合并 / 打开资源管理器时打开 / 右键菜单风格 / 开始菜单推荐项目行数).
/// </summary>
public interface IChoiceSetting
{
    string Id { get; }

    string Title { get; }

    string? Description { get; }

    TweakCategory Category { get; }

    EffectScope Effect { get; }

    IReadOnlyList<ChoiceOption> Options { get; }

    Task<int> GetSelectedIndexAsync(CancellationToken cancellationToken = default);

    Task<TweakApplyResult> ApplyAsync(int selectedIndex, CancellationToken cancellationToken = default);
}

/// <summary>
/// A choice backed by a single registry value. <paramref name="deleteValueForIndex"/> lets the
/// default option remove the value entirely, restoring the Windows default.
/// </summary>
public sealed class RegistryChoiceSetting(
    string id,
    string title,
    TweakCategory category,
    IRegistryService registry,
    string keyPath,
    string valueName,
    IReadOnlyList<ChoiceOption> options,
    int fallbackIndex = 0,
    int? deleteValueForIndex = null,
    Func<Task>? afterApply = null) : IChoiceSetting
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string? Description { get; init; }

    public TweakCategory Category { get; } = category;

    public EffectScope Effect { get; init; } = EffectScope.Immediate;

    public IReadOnlyList<ChoiceOption> Options { get; } = options;

    public Task<int> GetSelectedIndexAsync(CancellationToken cancellationToken = default)
    {
        var current = registry.GetInt(keyPath, valueName);
        if (current is null)
        {
            return Task.FromResult(fallbackIndex);
        }

        for (var i = 0; i < Options.Count; i++)
        {
            if (Options[i].Value == current)
            {
                return Task.FromResult(i);
            }
        }

        return Task.FromResult(fallbackIndex);
    }

    public async Task<TweakApplyResult> ApplyAsync(int selectedIndex, CancellationToken cancellationToken = default)
    {
        if (selectedIndex < 0 || selectedIndex >= Options.Count)
        {
            return TweakApplyResult.Fail("无效的选项。");
        }

        try
        {
            if (deleteValueForIndex == selectedIndex)
            {
                registry.DeleteValue(keyPath, valueName);
            }
            else
            {
                registry.SetInt(keyPath, valueName, Options[selectedIndex].Value);
            }

            if (afterApply is not null)
            {
                await afterApply().ConfigureAwait(false);
            }

            return TweakApplyResult.Ok();
        }
        catch (Exception ex)
        {
            return TweakApplyResult.Fail(ex.Message);
        }
    }
}

/// <summary>A choice whose read/write logic is fully supplied by the catalog.</summary>
public sealed class DelegateChoiceSetting(
    string id,
    string title,
    TweakCategory category,
    IReadOnlyList<ChoiceOption> options,
    Func<Task<int>> read,
    Func<int, Task<TweakApplyResult>> write) : IChoiceSetting
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string? Description { get; init; }

    public TweakCategory Category { get; } = category;

    public EffectScope Effect { get; init; } = EffectScope.Immediate;

    public IReadOnlyList<ChoiceOption> Options { get; } = options;

    public Task<int> GetSelectedIndexAsync(CancellationToken cancellationToken = default) => read();

    public Task<TweakApplyResult> ApplyAsync(int selectedIndex, CancellationToken cancellationToken = default) =>
        write(selectedIndex);
}
