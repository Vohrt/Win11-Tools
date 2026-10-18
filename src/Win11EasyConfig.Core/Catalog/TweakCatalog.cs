using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Services;
using Win11EasyConfig.Core.Settings;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Catalog;

/// <summary>
/// Builds the full set of settings (toggles, choices, numeric fields and one-shot actions).
/// This is the single place that knows every setting the app can change, so adding a feature
/// means adding data here rather than touching the UI.
/// </summary>
public sealed class TweakCatalog
{
    private readonly Lazy<IReadOnlyList<ITweak>> _tweaks;
    private readonly Lazy<IReadOnlyList<IChoiceSetting>> _choices;
    private readonly Lazy<IReadOnlyList<INumericSetting>> _numerics;
    private readonly Lazy<IReadOnlyList<IActionCommand>> _actions;

    public TweakCatalog(
        IRegistryService registry,
        ICommandRunner commandRunner,
        IWindowsServiceManager serviceManager,
        WindowsInfoService windowsInfo,
        ShellService shell)
    {
        _tweaks = new Lazy<IReadOnlyList<ITweak>>(() =>
        {
            var factory = new TweakFactory(registry);
            return
            [
                .. SystemTweaks.Create(factory, serviceManager),
                .. AdvancedTweaks.Create(factory, commandRunner),
                .. ExplorerTweaks.Create(factory, registry, windowsInfo),
                .. PrivacyTweaks.Create(factory, windowsInfo),
                .. PerformanceTweaks.Create(factory, commandRunner, serviceManager)
            ];
        });

        _choices = new Lazy<IReadOnlyList<IChoiceSetting>>(() =>
            [.. ExplorerExtras.CreateChoices(registry, shell)]);

        _numerics = new Lazy<IReadOnlyList<INumericSetting>>(() =>
            [.. PerformanceTweaks.CreateNumeric(commandRunner, registry)]);

        _actions = new Lazy<IReadOnlyList<IActionCommand>>(() =>
        [
            .. ExplorerExtras.CreateActions(commandRunner, shell),
            .. PrivacyExtras.CreateActions(commandRunner),
            .. PerformanceTweaks.CreateActions(commandRunner)
        ]);
    }

    public IReadOnlyList<ITweak> AllTweaks => _tweaks.Value;

    public IReadOnlyList<IChoiceSetting> AllChoices => _choices.Value;

    public IReadOnlyList<INumericSetting> AllNumerics => _numerics.Value;

    public IReadOnlyList<IActionCommand> AllActions => _actions.Value;

    public IReadOnlyList<ITweak> TweaksIn(TweakCategory category) =>
        [.. AllTweaks.Where(t => t.Category == category)];

    public IReadOnlyList<IChoiceSetting> ChoicesIn(TweakCategory category) =>
        [.. AllChoices.Where(c => c.Category == category)];

    public IReadOnlyList<INumericSetting> NumericsIn(TweakCategory category) =>
        [.. AllNumerics.Where(n => n.Category == category)];

    public IReadOnlyList<IActionCommand> ActionsIn(TweakCategory category) =>
        [.. AllActions.Where(a => a.Category == category)];

    public ITweak? FindTweak(string id) =>
        AllTweaks.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
}
