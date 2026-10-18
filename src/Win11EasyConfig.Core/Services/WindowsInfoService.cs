using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32;

namespace Win11EasyConfig.Core.Services;

public sealed record WindowsVersionInfo(
    int CurrentBuild,
    int UpdateBuildRevision,
    string EditionId,
    string DisplayVersion,
    string ProductName)
{
    public bool IsWindows11 => CurrentBuild >= 22000;

    public string FullBuild => $"{CurrentBuild}.{UpdateBuildRevision}";
}

/// <summary>Reads OS identity and elevation status.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsInfoService
{
    private const string CurrentVersionKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public WindowsVersionInfo GetVersion()
    {
        var build = ReadInt("CurrentBuild");
        var ubr = ReadInt("UBR");
        return new WindowsVersionInfo(
            build,
            ubr,
            ReadString("EditionID"),
            ReadString("DisplayVersion"),
            ReadString("ProductName"));
    }

    /// <summary>True when running on Windows 10 or later (the tool's supported range).</summary>
    public bool IsSupportedWindows() => Environment.OSVersion.Version.Major >= 10;

    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static int ReadInt(string name)
    {
        var value = Registry.GetValue(CurrentVersionKey, name, 0);
        return value is null ? 0 : int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static string ReadString(string name) =>
        Registry.GetValue(CurrentVersionKey, name, string.Empty)?.ToString() ?? string.Empty;
}
