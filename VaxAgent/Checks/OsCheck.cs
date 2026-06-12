using System;
using System.Management;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class OsCheck : ICheck
{
    public string Name => "OsCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context)
    {
        try
        {
#if NET35
            using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, ServicePackMajorVersion FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    context.Result.Os = obj["Caption"]?.ToString() ?? "Unknown OS";
                    string version = obj["Version"]?.ToString() ?? string.Empty;
                    string sp = obj["ServicePackMajorVersion"]?.ToString() ?? "0";
                    context.Result.PatchLevel = string.IsNullOrEmpty(version) ? null : $"Version {version} SP{sp}";
                    return CheckResult.Ok();
                }
            }
#else
            using var searcher = new ManagementObjectSearcher("SELECT Caption, Version, ServicePackMajorVersion FROM Win32_OperatingSystem");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                context.Result.Os = obj["Caption"]?.ToString() ?? "Unknown OS";
                string version = obj["Version"]?.ToString() ?? string.Empty;
                string sp = obj["ServicePackMajorVersion"]?.ToString() ?? "0";
                context.Result.PatchLevel = string.IsNullOrEmpty(version) ? null : $"Version {version} SP{sp}";
                return CheckResult.Ok();
            }
#endif
            context.Result.Os = "Unknown OS";
            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Queries WMI Win32_OperatingSystem for OS caption, version, and service pack level.
    // Returns CheckResult.Ok on success, modifying the context.Result.
}
