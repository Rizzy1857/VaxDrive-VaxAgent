using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VaxDrive.VaxAgent.Checks;

public interface IRegistryReader
{
    byte[]? ReadBinaryValue(string hive, string subkey, string valueName);
}

#if NET35
public class NativeRegistryReader : IRegistryReader
{
    private const uint HKEY_LOCAL_MACHINE = 0x80000002;
    private const int KEY_READ = 0x20019;

    [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
    private static extern int RegOpenKeyEx(
        uint hKey,
        string lpSubKey,
        uint ulOptions,
        int samDesired,
        out IntPtr phkResult);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
    private static extern int RegQueryValueEx(
        IntPtr hKey,
        string lpValueName,
        IntPtr lpReserved,
        out uint lpType,
        byte[] lpData,
        ref uint lpcbData);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(IntPtr hKey);

    public byte[]? ReadBinaryValue(string hive, string subkey, string valueName)
    {
        uint hKeyRoot = HKEY_LOCAL_MACHINE; // We only support HKLM for this task
        
        if (hive != "HKLM" && hive != "HKEY_LOCAL_MACHINE")
        {
            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Unsupported hive: {hive}");
            return null;
        }

        IntPtr hKey = IntPtr.Zero;
        try
        {
            int openResult = RegOpenKeyEx(hKeyRoot, subkey, 0, KEY_READ, out hKey);
            if (openResult != 0)
            {
                Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Failed to open registry key: {subkey}. Error: {openResult}");
                return null;
            }

            uint type;
            uint dataSize = 0;
            
            // First call to get size
            int queryResult = RegQueryValueEx(hKey, valueName, IntPtr.Zero, out type, null, ref dataSize);
            if (queryResult != 0 && queryResult != 234) // 234 is ERROR_MORE_DATA
            {
                Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Failed to query value size: {valueName}. Error: {queryResult}");
                return null;
            }

            byte[] data = new byte[dataSize];
            queryResult = RegQueryValueEx(hKey, valueName, IntPtr.Zero, out type, data, ref dataSize);
            
            if (queryResult != 0)
            {
                Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Failed to query value: {valueName}. Error: {queryResult}");
                return null;
            }

            return data;
        }
        finally
        {
            if (hKey != IntPtr.Zero)
            {
                RegCloseKey(hKey);
            }
        }
    }
}
#else
public class ManagedRegistryReader : IRegistryReader
{
    public byte[]? ReadBinaryValue(string hive, string subkey, string valueName)
    {
        if (hive != "HKLM" && hive != "HKEY_LOCAL_MACHINE")
        {
            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Unsupported hive: {hive}");
            return null;
        }

        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(subkey, writable: false);
            if (key == null)
            {
                Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Key not found: {subkey}");
                return null;
            }

            object? val = key.GetValue(valueName);
            if (val is byte[] bytes)
            {
                return bytes;
            }

            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Value {valueName} is not binary or not found.");
            return null;
        }
        catch (System.Security.SecurityException)
        {
            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Access denied to {subkey}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | RegistryReader | Error reading {subkey}: {ex.Message}");
            return null;
        }
    }
}
#endif
