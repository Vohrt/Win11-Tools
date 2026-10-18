using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Win11EasyConfig.Core.Services;

/// <summary>
/// Shell-level operations (icon cache, Explorer restart, desktop refresh) via Win32 calls.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ShellService
{
    [Flags]
    private enum RecycleFlags : uint
    {
        NoConfirmation = 0x1,
        NoProgressUi = 0x2,
        NoSound = 0x4
    }

    private const int ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, RecycleFlags flags);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    /// <summary>Empties the recycle bin without confirmation, progress UI or sound.</summary>
    public void EmptyRecycleBin() =>
        SHEmptyRecycleBin(
            IntPtr.Zero,
            null,
            RecycleFlags.NoConfirmation | RecycleFlags.NoProgressUi | RecycleFlags.NoSound);

    /// <summary>Notifies the shell that file associations/icons changed (刷新系统图标缓存).</summary>
    public void RefreshIconCache() => SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);

    /// <summary>
    /// Kills every explorer.exe instance. Windows restarts the shell automatically, which is
    /// how settings that only take effect after an Explorer restart are applied.
    /// </summary>
    public void RestartExplorer()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // A protected instance may refuse to exit; the remaining ones still restart.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
