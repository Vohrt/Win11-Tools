using Win11EasyConfig.Core.Catalog;

namespace Win11EasyConfig.Core.Tests;

public class RightClickMenuTests
{
    private const string Original = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
    private const string Updated = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{51668040-5e31-4023-bbe3-149163bd80f1}\InprocServer32";
    private const string Machine = @"HKEY_LOCAL_MACHINE\Software\Classes\CLSID\{51668040-5e31-4023-bbe3-149163bd80f1}\InprocServer32";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Classic_and_modern_round_trip_on_registered_handlers(bool updatedHandler)
    {
        var token = TestContext.Current.CancellationToken;
        var registry = new FakeRegistryService();
        if (updatedHandler)
        {
            registry.SetRaw(Machine, "", "Windows.UI.FileExplorer.dll");
        }

        registry.SetRaw(Original, "ThreadingModel", "Apartment");
        int restarts = 0;
        var menu = ExplorerExtras.CreateRightClickMenu(registry, () => restarts++);

        Assert.Equal(0, await menu.GetSelectedIndexAsync(token));
        Assert.True((await menu.ApplyAsync(1, token)).Success);
        Assert.Equal("", registry.GetString(Original, ""));
        Assert.Equal(updatedHandler ? "" : null, registry.GetString(Updated, ""));
        Assert.Equal(1, await menu.GetSelectedIndexAsync(token));

        Assert.True((await menu.ApplyAsync(0, token)).Success);
        Assert.Null(registry.GetString(Original, ""));
        Assert.Null(registry.GetString(Updated, ""));
        Assert.Equal("Apartment", registry.GetString(Original, "ThreadingModel"));
        Assert.Equal(0, await menu.GetSelectedIndexAsync(token));
        Assert.Equal(2, restarts);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, -1)]
    public async Task Existing_single_override_can_be_read_and_reapplied(bool updatedHandler, int expected)
    {
        var token = TestContext.Current.CancellationToken;
        var registry = new FakeRegistryService();
        if (updatedHandler)
        {
            registry.SetRaw(Machine, "", "Windows.UI.FileExplorer.dll");
        }

        registry.SetRaw(Original, "", "");
        var menu = ExplorerExtras.CreateRightClickMenu(registry, () => { });

        Assert.Equal(expected, await menu.GetSelectedIndexAsync(token));
        Assert.Empty(registry.Writes);
        Assert.True((await menu.ApplyAsync(1, token)).Success);
        Assert.Equal(1, await menu.GetSelectedIndexAsync(token));
    }

    [Fact]
    public async Task Restoring_modern_from_partial_state_preserves_nonempty_registration()
    {
        var token = TestContext.Current.CancellationToken;
        var registry = new FakeRegistryService();
        registry.SetRaw(Machine, "", "Windows.UI.FileExplorer.dll");
        registry.SetRaw(Original, "", "CustomMenu.dll");
        registry.SetRaw(Updated, "", "");
        var menu = ExplorerExtras.CreateRightClickMenu(registry, () => { });

        Assert.Equal(-1, await menu.GetSelectedIndexAsync(token));
        Assert.False((await menu.ApplyAsync(1, token)).Success);
        Assert.Empty(registry.Writes);
        Assert.True((await menu.ApplyAsync(0, token)).Success);
        Assert.Equal(0, await menu.GetSelectedIndexAsync(token));
        Assert.Equal("CustomMenu.dll", registry.GetString(Original, ""));
        Assert.Null(registry.GetString(Updated, ""));
    }
}
