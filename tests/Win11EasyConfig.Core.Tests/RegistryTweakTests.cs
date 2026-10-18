using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Tests;

/// <summary>
/// The declarative registry engine: probe semantics and the writes produced by the three
/// factory patterns used across the catalog.
/// </summary>
public class RegistryTweakTests
{
    private const string Key = @"HKEY_CURRENT_USER\Software\Test\Key";

    [Fact]
    public async Task Value_probe_uses_the_default_when_the_value_is_missing()
    {
        var registry = new FakeRegistryService();
        var tweak = new TweakFactory(registry)
            .Value("test.value", "T", TweakCategory.System, Key, "V", onValue: 1, offValue: 0, defaultWhenMissing: 1);

        Assert.Equal(TweakState.On, await tweak.GetStateAsync(TestContext.Current.CancellationToken));

        registry.SetRaw(Key, "V", 0);
        Assert.Equal(TweakState.Off, await tweak.GetStateAsync(TestContext.Current.CancellationToken));

        registry.SetRaw(Key, "V", 1);
        Assert.Equal(TweakState.On, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Value_probe_is_unknown_when_missing_and_no_default_is_declared()
    {
        var registry = new FakeRegistryService();
        var tweak = new TweakFactory(registry)
            .Value("test.value", "T", TweakCategory.System, Key, "V", onValue: 1, offValue: 0);

        Assert.Equal(TweakState.Unknown, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Value_apply_writes_the_matching_on_and_off_values()
    {
        var registry = new FakeRegistryService();
        var tweak = new TweakFactory(registry)
            .Value("test.value", "T", TweakCategory.System, Key, "V", onValue: 1, offValue: 0);

        Assert.True((await tweak.ApplyAsync(true, TestContext.Current.CancellationToken)).Success);
        Assert.Contains(registry.Writes, w => w.KeyPath == Key && w.ValueName == "V" && (int)w.Value! == 1);
        Assert.Equal(TweakState.On, await tweak.GetStateAsync(TestContext.Current.CancellationToken));

        Assert.True((await tweak.ApplyAsync(false, TestContext.Current.CancellationToken)).Success);
        Assert.Contains(registry.Writes, w => w.KeyPath == Key && w.ValueName == "V" && (int)w.Value! == 0);
        Assert.Equal(TweakState.Off, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Policy_probe_treats_a_missing_value_as_on()
    {
        var registry = new FakeRegistryService();
        var tweak = new TweakFactory(registry)
            .Policy("test.policy", "T", TweakCategory.Privacy, Key, "P", offValue: 0);

        Assert.Equal(TweakState.On, await tweak.GetStateAsync(TestContext.Current.CancellationToken));

        registry.SetRaw(Key, "P", 0);
        Assert.Equal(TweakState.Off, await tweak.GetStateAsync(TestContext.Current.CancellationToken));

        registry.SetRaw(Key, "P", 5);
        Assert.Equal(TweakState.On, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Policy_apply_on_deletes_the_value_and_off_writes_the_policy()
    {
        var registry = new FakeRegistryService();
        var tweak = new TweakFactory(registry)
            .Policy("test.policy", "T", TweakCategory.Privacy, Key, "P", offValue: 0);

        Assert.True((await tweak.ApplyAsync(false, TestContext.Current.CancellationToken)).Success);
        Assert.Contains(registry.Writes, w => w.KeyPath == Key && w.ValueName == "P" && (int)w.Value! == 0);
        Assert.Equal(TweakState.Off, await tweak.GetStateAsync(TestContext.Current.CancellationToken));

        Assert.True((await tweak.ApplyAsync(true, TestContext.Current.CancellationToken)).Success);
        Assert.Contains(registry.Deletes, d => d.KeyPath == Key && d.ValueName == "P");
        Assert.Equal(TweakState.On, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Custom_apply_supports_binary_writes_and_key_tree_deletes()
    {
        var registry = new FakeRegistryService();
        var tweak = new TweakFactory(registry)
            .Custom(
                "test.custom", "T", TweakCategory.Explorer,
                _ => TweakState.Off,
                onWrites: [RegistryWrite.Binary(Key, "B", [1, 2, 3])],
                offWrites: [RegistryWrite.DeleteTree(Key)]);

        Assert.True((await tweak.ApplyAsync(true, TestContext.Current.CancellationToken)).Success);
        var write = Assert.Single(registry.Writes);
        Assert.Equal("B", write.ValueName);
        Assert.Equal([1, 2, 3], (byte[])write.Value!);

        Assert.True((await tweak.ApplyAsync(false, TestContext.Current.CancellationToken)).Success);
        Assert.Contains(Key, registry.DeletedTrees);
    }
}
