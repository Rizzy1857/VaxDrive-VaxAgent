using System;
using Microsoft.Win32;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class InstalledSoftwareCheck : ICheck
{
    public string Name => "InstalledSoftwareCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context)
    {
        try
        {
            // 64-bit hive (or native 32-bit on 32-bit OS)
            ExtractFromRegistryKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", context);

            // 32-bit hive on 64-bit OS
            if (IntPtr.Size == 8) // Simple check for 64-bit process
            {
                ExtractFromRegistryKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", context);
            }

            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Reads uninstall keys from the registry to build a list of installed software.
    // Returns CheckResult.Ok on success, populating context.InstalledSoftware.

    private void ExtractFromRegistryKey(string path, ScanContext context)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
        if (key == null) return;

        foreach (string subkeyName in key.GetSubKeyNames())
        {
            using RegistryKey? subkey = key.OpenSubKey(subkeyName, writable: false);
            if (subkey == null) continue;

            string displayName = subkey.GetValue("DisplayName")?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(displayName)) continue; // Skip unnamed components

            string displayVersion = subkey.GetValue("DisplayVersion")?.ToString() ?? string.Empty;
            string publisher = subkey.GetValue("Publisher")?.ToString() ?? string.Empty;
            string installDate = subkey.GetValue("InstallDate")?.ToString() ?? string.Empty;

            context.InstalledSoftware.Add(new SoftwareEntry
            {
                DisplayName = displayName,
                DisplayVersion = displayVersion,
                Publisher = publisher,
                InstallDate = installDate
            });
        }
    }
    // Enumerate a specific registry path, reading DisplayName and other properties.
    // Populates the context.InstalledSoftware list directly. Returns void.
}
