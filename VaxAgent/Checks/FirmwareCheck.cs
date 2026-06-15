using System;
using System.Management;
using System.Threading;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class FirmwareCheck : ICheck
{
    public string Name => "FirmwareCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        try
        {
            FirmwareRecord record = GetFirmwareRecord();
            string computerName = Environment.MachineName;

            context.Result.Firmware = record;

            string biosString = $"{record.Manufacturer} {record.SMBIOSBIOSVersion} {record.ReleaseDate} {record.SerialNumber}";
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

    internal static FirmwareRecord GetFirmwareRecord()
    {
        var record = new FirmwareRecord();
        int missingCount = 0;
        bool found = false;

#if NET35
        using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS"))
        {
            foreach (ManagementObject obj in searcher.Get())
            {
                record.Manufacturer = ParseField(obj, "Manufacturer", ref missingCount);
                record.SMBIOSBIOSVersion = ParseField(obj, "SMBIOSBIOSVersion", ref missingCount);
                record.ReleaseDate = ParseField(obj, "ReleaseDate", ref missingCount);
                record.SerialNumber = ParseField(obj, "SerialNumber", ref missingCount);
                found = true;
                break;
            }
        }
#else
        using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS");
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            record.Manufacturer = ParseField(obj, "Manufacturer", ref missingCount);
            record.SMBIOSBIOSVersion = ParseField(obj, "SMBIOSBIOSVersion", ref missingCount);
            record.ReleaseDate = ParseField(obj, "ReleaseDate", ref missingCount);
            record.SerialNumber = ParseField(obj, "SerialNumber", ref missingCount);
            found = true;
            break;
        }
#endif
        if (!found)
        {
            missingCount = 4;
            record.Manufacturer = "UNKNOWN_Manufacturer";
            record.SMBIOSBIOSVersion = "UNKNOWN_SMBIOSBIOSVersion";
            record.ReleaseDate = "UNKNOWN_ReleaseDate";
            record.SerialNumber = "UNKNOWN_SerialNumber";
        }

        record.ConfidenceScore = CalculateConfidence(missingCount);
        
        Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | [FIRMWARE] confidence={record.ConfidenceScore:F2} fields={record.Manufacturer}|{record.SMBIOSBIOSVersion}|{record.ReleaseDate}|{record.SerialNumber}");

        return record;
    }

    internal static double CalculateConfidence(int missingCount)
    {
        return Math.Max(0.0, 1.0 - (missingCount * 0.25));
    }
    
    internal static string ParseField(ManagementBaseObject obj, string fieldName, ref int missingCount)
    {
        string? val = null;
        try
        {
            val = obj[fieldName]?.ToString();
        }
        catch { } // Field doesn't exist or other error

        if (string.IsNullOrEmpty(val))
        {
            missingCount++;
            return $"UNKNOWN_{fieldName}";
        }
        return val;
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
