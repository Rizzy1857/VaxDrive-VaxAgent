using System;
using Microsoft.Win32;
using System.Threading;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class InstalledSoftwareCheck : ICheck
{
    public string Name => "InstalledSoftwareCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        try
        {
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Native 64-bit hive (or 32-bit on 32-bit OS)
            ExtractFromRegistryKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", context, seen);

            // 32-bit hive on 64-bit OS
            if (IntPtr.Size == 8)
            {
                ExtractFromRegistryKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", context, seen);
            }

            // ARM detection
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? string.Empty;
            string archw6432 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? string.Empty;
            
            if (arch.Contains("ARM") || archw6432.Contains("ARM"))
            {
                Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | InstalledSoftwareCheck | ARM architecture detected. Querying ARM hive.");
                ExtractFromRegistryKey(@"SOFTWARE\ARM\Microsoft\Windows\CurrentVersion\Uninstall", context, seen);
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

    internal static void ExtractFromRegistryKey(string path, ScanContext context, System.Collections.Generic.HashSet<string> seen)
    {
        try
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

                string uniqueKey = $"{displayName}::{displayVersion}";
                if (seen.Add(uniqueKey))
                {
                    context.InstalledSoftware.Add(new SoftwareEntry
                    {
                        DisplayName = displayName,
                        DisplayVersion = displayVersion,
                        Publisher = publisher,
                        InstallDate = installDate
                    });
                }
            }
        }
        catch (System.Security.SecurityException)
        {
            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | InstalledSoftwareCheck | Warning: Access denied to {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | InstalledSoftwareCheck | Warning: Failed to query {path}: {ex.Message}");
        }
    }
    // Enumerate a specific registry path, reading DisplayName and other properties.
    // Populates the context.InstalledSoftware list directly with deduplication. Returns void.
}
