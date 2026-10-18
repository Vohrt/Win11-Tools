namespace Win11EasyConfig.Core.Abstractions;

/// <summary>
/// Registry access expressed with full "HKEY_..." paths, mirroring how the tweak
/// catalog is authored.
/// </summary>
public interface IRegistryService
{
    int? GetInt(string keyPath, string valueName);

    string? GetString(string keyPath, string valueName);

    byte[]? GetBinary(string keyPath, string valueName);

    void SetInt(string keyPath, string valueName, int value);

    void SetString(string keyPath, string valueName, string value);

    void SetBinary(string keyPath, string valueName, byte[] value);

    /// <summary>Deletes a single value; missing values are ignored.</summary>
    void DeleteValue(string keyPath, string valueName);

    /// <summary>Deletes a key and everything under it; missing keys are ignored.</summary>
    void DeleteKeyTree(string keyPath);

    /// <summary>Returns the immediate child key names, or an empty array if the key is missing.</summary>
    IReadOnlyList<string> GetSubKeyNames(string keyPath);
}
