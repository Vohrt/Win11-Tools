using Microsoft.Win32;
using Win11EasyConfig.Core.Abstractions;

namespace Win11EasyConfig.Core.Services;

/// <summary>
/// Registry access built on <see cref="Registry"/>. All operations are defensive: a missing
/// key or value yields null rather than throwing, which matches how the tweak probes are written.
/// </summary>
public sealed class RegistryService : IRegistryService
{
    public int? GetInt(string keyPath, string valueName)
    {
        var value = GetValue(keyPath, valueName);
        return value switch
        {
            int i => i,
            uint u => unchecked((int)u),
            long l => unchecked((int)l),
            _ => null
        };
    }

    public string? GetString(string keyPath, string valueName) => GetValue(keyPath, valueName) as string;

    public byte[]? GetBinary(string keyPath, string valueName) => GetValue(keyPath, valueName) as byte[];

    public void SetInt(string keyPath, string valueName, int value) =>
        SetValue(keyPath, valueName, value, RegistryValueKind.DWord);

    public void SetString(string keyPath, string valueName, string value) =>
        SetValue(keyPath, valueName, value, RegistryValueKind.String);

    public void SetBinary(string keyPath, string valueName, byte[] value) =>
        SetValue(keyPath, valueName, value, RegistryValueKind.Binary);

    public void DeleteValue(string keyPath, string valueName)
    {
        var (hive, subKey) = Split(keyPath);
        if (hive is null)
        {
            return;
        }

        using var key = hive.OpenSubKey(subKey, writable: true);
        if (key is null)
        {
            return;
        }

        try
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch (UnauthorizedAccessException)
        {
            // Value is protected (e.g. by UCPD); treated as a no-op so callers can report failure via state re-read.
        }
    }

    public void DeleteKeyTree(string keyPath)
    {
        var (hive, subKey) = Split(keyPath);
        if (hive is null || string.IsNullOrEmpty(subKey))
        {
            return;
        }

        try
        {
            hive.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public IReadOnlyList<string> GetSubKeyNames(string keyPath)
    {
        var (hive, subKey) = Split(keyPath);
        if (hive is null)
        {
            return [];
        }

        try
        {
            using var key = hive.OpenSubKey(subKey);
            return key is null ? [] : key.GetSubKeyNames();
        }
        catch
        {
            return [];
        }
    }

    private static object? GetValue(string keyPath, string valueName)
    {
        try
        {
            return Registry.GetValue(keyPath, valueName, null);
        }
        catch
        {
            return null;
        }
    }

    private static void SetValue(string keyPath, string valueName, object value, RegistryValueKind kind)
    {
        var (hive, subKey) = Split(keyPath);
        if (hive is null)
        {
            throw new ArgumentException($"Unsupported registry root in '{keyPath}'.", nameof(keyPath));
        }

        using var key = hive.CreateSubKey(subKey, writable: true)
                        ?? throw new InvalidOperationException($"Could not open or create '{keyPath}'.");
        key.SetValue(valueName, value, kind);
    }

    private static (RegistryKey? Hive, string SubKey) Split(string keyPath)
    {
        var separator = keyPath.IndexOf('\\');
        var rootName = separator < 0 ? keyPath : keyPath[..separator];
        var subKey = separator < 0 ? string.Empty : keyPath[(separator + 1)..];

        RegistryKey? hive = rootName.ToUpperInvariant() switch
        {
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" => Registry.Users,
            "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => null
        };

        return (hive, subKey);
    }
}
