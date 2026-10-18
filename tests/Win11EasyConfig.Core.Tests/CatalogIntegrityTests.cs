using Win11EasyConfig.Core.Models;

namespace Win11EasyConfig.Core.Tests;

/// <summary>
/// Structural checks on the declarative catalog: counts, id uniqueness, interlock references and
/// the read-only / unsupported contracts the UI relies on. These run without touching the machine
/// (the catalog only reads OS build info lazily where a tweak is version-gated).
/// </summary>
public class CatalogIntegrityTests
{
    private static readonly Catalog.TweakCatalog Catalog = TestCatalog.Create();

    [Fact]
    public void Totals_match_the_documented_set()
    {
        Assert.Equal(57, Catalog.AllTweaks.Count);
        Assert.Equal(6, Catalog.AllChoices.Count);
        Assert.Equal(2, Catalog.AllNumerics.Count);
        Assert.Equal(25, Catalog.AllActions.Count);
    }

    [Fact]
    public void Category_distribution_matches_the_original()
    {
        Assert.Equal(8, Catalog.TweaksIn(TweakCategory.System).Count);
        Assert.Equal(7, Catalog.TweaksIn(TweakCategory.Advanced).Count);
        Assert.Equal(14, Catalog.TweaksIn(TweakCategory.Explorer).Count);
        Assert.Equal(6, Catalog.TweaksIn(TweakCategory.Taskbar).Count);
        Assert.Equal(11, Catalog.TweaksIn(TweakCategory.Privacy).Count);
        Assert.Equal(10, Catalog.TweaksIn(TweakCategory.Performance).Count);
        Assert.Single(Catalog.TweaksIn(TweakCategory.Network));
    }

    [Fact]
    public void All_setting_ids_are_unique()
    {
        var ids = Catalog.AllTweaks.Select(t => t.Id)
            .Concat(Catalog.AllChoices.Select(c => c.Id))
            .Concat(Catalog.AllNumerics.Select(n => n.Id))
            .Concat(Catalog.AllActions.Select(a => a.Id))
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_requiresOn_reference_points_to_an_existing_tweak()
    {
        var ids = Catalog.AllTweaks.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tweak in Catalog.AllTweaks)
        {
            foreach (var required in tweak.RequiresOn)
            {
                Assert.Contains(required, ids);
            }
        }

        foreach (var numeric in Catalog.AllNumerics)
        {
            foreach (var required in numeric.RequiresOn)
            {
                Assert.Contains(required, ids);
            }
        }
    }

    [Fact]
    public void Auto_hide_taskbar_is_present_and_restart_scoped()
    {
        var tweak = Catalog.FindTweak("taskbar.autoHide");

        Assert.NotNull(tweak);
        Assert.Equal("自动隐藏任务栏", tweak!.Title);
        Assert.Equal(TweakCategory.Taskbar, tweak.Category);
        Assert.Equal(EffectScope.RestartExplorer, tweak.Effect);
        Assert.False(tweak.IsReadOnly);
    }

    [Fact]
    public void Interlocks_match_the_original_dialog()
    {
        Assert.Equal(["performance.hibernate"], Catalog.FindTweak("performance.fastStartup")!.RequiresOn);

        foreach (var id in new[]
                 {
                     "performance.memoryCompression", "performance.appPrelaunch",
                     "performance.appPrefetch", "performance.pageCombining"
                 })
        {
            Assert.Equal(["performance.sysMain"], Catalog.FindTweak(id)!.RequiresOn);
        }

        var maxPrefetch = Catalog.AllNumerics.Single(n => n.Id == "performance.maxPrefetchFiles");
        Assert.Equal(["performance.sysMain"], maxPrefetch.RequiresOn);
    }

    [Fact]
    public void Only_the_app_prefetch_probe_is_read_only()
    {
        Assert.Equal(
            ["performance.appPrefetch"],
            Catalog.AllTweaks.Where(t => t.IsReadOnly).Select(t => t.Id));
    }

    [Fact]
    public void Unsupported_tweaks_carry_an_explanation()
    {
        // Version-gated rows (win10Style on build 22000) must say why they are disabled.
        foreach (var tweak in Catalog.AllTweaks.Where(t => !t.IsSupported))
        {
            Assert.False(string.IsNullOrWhiteSpace(tweak.UnsupportedReason));
        }
    }

    [Fact]
    public void Every_setting_has_a_title()
    {
        Assert.All(Catalog.AllTweaks, t => Assert.False(string.IsNullOrWhiteSpace(t.Title)));
        Assert.All(Catalog.AllChoices, c => Assert.False(string.IsNullOrWhiteSpace(c.Title)));
        Assert.All(Catalog.AllNumerics, n => Assert.False(string.IsNullOrWhiteSpace(n.Title)));
        Assert.All(Catalog.AllActions, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Title));
            Assert.False(string.IsNullOrWhiteSpace(a.GroupName));
        });
    }

    [Fact]
    public void Numeric_ranges_match_the_original()
    {
        var prefetch = Catalog.AllNumerics.Single(n => n.Id == "performance.maxPrefetchFiles");
        Assert.Equal(1, prefetch.Minimum);
        Assert.Equal(8192, prefetch.Maximum);

        var rdpPort = Catalog.AllNumerics.Single(n => n.Id == "performance.rdpPort");
        Assert.Equal(1, rdpPort.Minimum);
        Assert.Equal(65535, rdpPort.Maximum);
    }
}
