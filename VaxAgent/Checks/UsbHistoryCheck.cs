using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Threading;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class UsbHistoryCheck : ICheck
{
    public string Name => "UsbHistoryCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USBSTOR", writable: false);
            if (key == null) return CheckResult.Ok(); // No USBSTOR key means no mass storage history

            // TODO: Load allowlist from Definitions pack via DefinitionLoader once implemented
            // Using a stub list for Phase 1
            var allowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Kingston", // VaxDrive itself
                "VMware",   // Emulated drives
                "Logitech", // Common peripherals
                "Microsoft"
            };

#if NET35
            IRegistryReader reader = new NativeRegistryReader();
#else
            IRegistryReader reader = new ManagedRegistryReader();
#endif

            foreach (string subkeyName in key.GetSubKeyNames())
            {
                string deviceDesc = subkeyName;
                if (deviceDesc.StartsWith("Disk&", StringComparison.OrdinalIgnoreCase))
                {
                    deviceDesc = deviceDesc.Substring(5); // Strip "Disk&" prefix
                }

                bool isAllowed = false;
                foreach (string allowed in allowList)
                {
                    if (deviceDesc.Contains(allowed))
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (!isAllowed)
                {
                    string propPath = $@"SYSTEM\CurrentControlSet\Enum\USBSTOR\{subkeyName}\Properties\{{83da6326-97a6-4088-9453-a1923f573b29}}\0065";
                    byte[]? fileTimeBytes = reader.ReadBinaryValue("HKLM", propPath, "00000000"); // 0065 is the key, value is typically "00000000" or "(Default)"
                    // Wait, what is the value name? The key is \0065, and the binary value is the (Default) value? Or is 0065 the value name in the Properties key?
                    // Actually, in Windows 8+, Properties\{GUID}\0065 contains a value. The value name is usually "00000000" or the default value.
                    // Wait, the prompt says "P/Invoke RegQueryValueEx to read binary FILETIME from registry HKLM\SYSTEM\CurrentControlSet\Enum\USBSTOR\<device>\Properties\{83da...}\0065".
                    // If 0065 is a key, the value is probably named "00000000" or "". I'll query "". 
                    // Let's query "" (default value) or "00000000". Wait, if 0065 is a value, then {83da...} is the key and 0065 is the value name.
                    // "Registry path: HKLM\SYSTEM\CurrentControlSet\Enum\USBSTOR\<device>\Properties\{83da6326-97a6-4088-9453-a1923f573b29}\0065"
                    // If 0065 is a value name, then the subkey is up to the GUID.
                    // Let's assume the key is the GUID and the value name is "0065".
                    // Let's check "0065" as the value name.
                    
                    string guidKey = $@"SYSTEM\CurrentControlSet\Enum\USBSTOR\{subkeyName}\Properties\{{83da6326-97a6-4088-9453-a1923f573b29}}";
                    byte[]? bytes = reader.ReadBinaryValue("HKLM", guidKey, "0065");
                    
                    if (bytes == null)
                    {
                        // Some systems store it as a subkey \0065 with value "00000000"
                        bytes = reader.ReadBinaryValue("HKLM", guidKey + @"\0065", "00000000");
                        if (bytes == null)
                        {
                            bytes = reader.ReadBinaryValue("HKLM", guidKey + @"\0065", "");
                        }
                    }

                    string timeStr = "Unknown Time";
                    if (bytes != null && bytes.Length >= 8)
                    {
                        try
                        {
                            long fileTime = BitConverter.ToInt64(bytes, 0);
                            DateTime dt = DateTime.FromFileTimeUtc(fileTime);
                            timeStr = dt.ToString("O");
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            // Invalid file time
                        }
                    }
                    
                    context.Result.UsbAnomalies.Add($"Historical USB Device: {deviceDesc} (First Install: {timeStr})");
                }
            }

            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Enumerates the USBSTOR registry key to find historical mass storage devices, flagging unknown ones.
    // Returns CheckResult.Ok on success, appending anomalies to context.Result.UsbAnomalies.
}
