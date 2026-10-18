using Win11EasyConfig.Core.Features;

namespace Win11EasyConfig.Core.Tests;

/// <summary>
/// Guards the full store-app catalog (61 entries with their Store ids and package prefixes).
/// </summary>
public class StoreCatalogTests
{
    [Fact]
    public void Catalog_contains_all_61_apps_from_the_original()
    {
        Assert.Equal(61, AppsService.Catalog.Count);
    }

    [Fact]
    public void Names_are_unique_and_prefixes_are_present()
    {
        var names = AppsService.Catalog.Select(a => a.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());

        Assert.All(AppsService.Catalog, app => Assert.False(string.IsNullOrWhiteSpace(app.PackagePrefix)));
    }

    [Fact]
    public void Store_ids_are_unique_where_present()
    {
        var storeIds = AppsService.Catalog
            .Where(a => a.CanInstallFromStore)
            .Select(a => a.StoreId)
            .ToList();

        Assert.Equal(storeIds.Count, storeIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Xbox_speech_overlay_is_detectable_but_not_installable()
    {
        var app = AppsService.Catalog.Single(a => a.PackagePrefix == "Microsoft.XboxSpeechToTextOverlay_");

        Assert.False(app.CanInstallFromStore);
        Assert.Equal(string.Empty, app.StoreId);
    }

    [Fact]
    public void Presence_detection_matches_the_package_family_prefix()
    {
        var installed = new[]
        {
            "Microsoft.Windows.Photos_2026.1001.0.0_x64__8wekyb3d8bbwe",
            "Microsoft.PowerToys_0.9.0.0_x64__8wekyb3d8bbwe"
        };

        Assert.True(AppsService.IsPresent(AppsService.Catalog.Single(a => a.Name == "照片"), installed));
        Assert.True(AppsService.IsPresent(AppsService.Catalog.Single(a => a.Name == "PowerToys"), installed));
        Assert.False(AppsService.IsPresent(AppsService.Catalog.Single(a => a.Name == "计算器"), installed));
    }
}
