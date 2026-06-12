using System;
using System.Management;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class FirmwareCheck : ICheck
{
    public string Name => "FirmwareCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context)
    {
        try
        {
            string biosString = GetBiosString();
            string computerName = Environment.MachineName;

            context.Result.DeviceFingerprint = GenerateFingerprint(computerName, biosString);

            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Runs the FirmwareCheck to extract BIOS strings and generate the device fingerprint.
    // Returns a CheckResult indicating success or capturing the failure exception.

    private string GetBiosString()
    {
#if NET35
        using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS"))
        {
            foreach (ManagementObject obj in searcher.Get())
            {
                string manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                string version = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                return $"{manufacturer} {version}";
            }
        }
#else
        using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS");
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            string manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
            string version = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
            return $"{manufacturer} {version}";
        }
#endif
        return "Unknown BIOS";
    }
    // Queries WMI Win32_BIOS to extract the Manufacturer and Version.
    // Returns a concatenated BIOS string.

    private string GetComputerName()
    {
        return Environment.MachineName;
    }
    // Retrieves the local machine's NetBIOS name.
    // Returns the NetBIOS name as a string.

    private string GenerateFingerprint(string computerName, string biosString)
    {
        string rawData = computerName + biosString;
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(rawData);
        byte[] hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
    }
    // Computes a stable SHA256 hash of the computer name and BIOS string to use as a unique device identifier.
    // Returns a 16-character hex string representing the fingerprint.
}
