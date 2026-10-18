using Win11EasyConfig.Core.Models;
using Win11EasyConfig.Core.Tweaks;

namespace Win11EasyConfig.Core.Tests;

/// <summary>
/// Behaviour of 自动隐藏任务栏, which patches bit0 of byte 8 in the StuckRects3 "Settings" blob.
/// </summary>
public class AutoHideTaskbarTweakTests
{
    private const string Key =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";

    private static (ITweak Tweak, FakeRegistryService Registry) CreateTweak()
    {
        var registry = new FakeRegistryService();
        var catalog = TestCatalog.Create(registry);
        return (catalog.FindTweak("taskbar.autoHide")!, registry);
    }

    /// <summary>A plausible StuckRects3 blob: 12 bytes with the auto-hide flag at index 8.</summary>
    private static byte[] MakeSettings(byte flag) =>
        [0x30, 0x00, 0x00, 0x00, 0xFE, 0xFF, 0xFF, 0xFF, flag, 0x03, 0x00, 0x00];

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(0x2B)]
    public async Task GetState_is_on_for_an_odd_flag_byte(byte flag)
    {
        var (tweak, registry) = CreateTweak();
        registry.SetRaw(Key, "Settings", MakeSettings(flag));

        Assert.Equal(TweakState.On, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(0xFC)]
    public async Task GetState_is_off_for_an_even_flag_byte(byte flag)
    {
        var (tweak, registry) = CreateTweak();
        registry.SetRaw(Key, "Settings", MakeSettings(flag));

        Assert.Equal(TweakState.Off, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetState_is_unknown_without_a_settings_blob()
    {
        var (tweak, _) = CreateTweak();

        Assert.Equal(TweakState.Unknown, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetState_is_unknown_for_a_truncated_blob()
    {
        var (tweak, registry) = CreateTweak();
        registry.SetRaw(Key, "Settings", new byte[8]);

        Assert.Equal(TweakState.Unknown, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Enable_flips_the_flag_bit_and_preserves_the_rest_of_the_blob()
    {
        var (tweak, registry) = CreateTweak();
        var before = MakeSettings(flag: 2);
        registry.SetRaw(Key, "Settings", before);

        var result = await tweak.ApplyAsync(enable: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var write = Assert.Single(registry.Writes);
        Assert.Equal(Key, write.KeyPath);
        Assert.Equal("Settings", write.ValueName);

        var written = Assert.IsType<byte[]>(write.Value);
        Assert.Equal(12, written.Length);
        Assert.Equal(3, written[8]); // 2 -> 3 (odd flips the flag on)
        for (var i = 0; i < before.Length; i++)
        {
            if (i != 8)
            {
                Assert.Equal(before[i], written[i]);
            }
        }

        Assert.Equal(TweakState.On, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disable_clears_the_flag_bit()
    {
        var (tweak, registry) = CreateTweak();
        registry.SetRaw(Key, "Settings", MakeSettings(flag: 3));

        var result = await tweak.ApplyAsync(enable: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var written = Assert.IsType<byte[]>(Assert.Single(registry.Writes).Value);
        Assert.Equal(2, written[8]); // 3 -> 2 (even flips the flag off)
        Assert.Equal(TweakState.Off, await tweak.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Applying_an_unchanged_state_does_not_write()
    {
        var (tweak, registry) = CreateTweak();
        registry.SetRaw(Key, "Settings", MakeSettings(flag: 3));

        var result = await tweak.ApplyAsync(enable: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(registry.Writes);
    }

    [Fact]
    public async Task Apply_fails_without_a_settings_blob()
    {
        var (tweak, registry) = CreateTweak();

        var result = await tweak.ApplyAsync(enable: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Empty(registry.Writes);
    }

    [Fact]
    public async Task Apply_fails_for_a_truncated_blob()
    {
        var (tweak, registry) = CreateTweak();
        registry.SetRaw(Key, "Settings", new byte[5]);

        var result = await tweak.ApplyAsync(enable: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Empty(registry.Writes);
    }
}
