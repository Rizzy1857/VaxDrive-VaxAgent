using System;
using System.Collections.Generic;
using Microsoft.Win32;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class UsbHistoryCheck : ICheck
{
    public string Name => "UsbHistoryCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context)
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
                    context.Result.UsbAnomalies.Add($"Historical USB Device: {deviceDesc}");
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
