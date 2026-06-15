using System;
using System.Management;
using System.Threading;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class ServicesCheck : ICheck
{
    public string Name => "ServicesCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        try
        {
#if NET35
            using (var searcher = new ManagementObjectSearcher("SELECT Name, State, StartMode, PathName FROM Win32_Service"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    context.Services.Add(new ServiceEntry
                    {
                        Name = obj["Name"]?.ToString() ?? string.Empty,
                        State = obj["State"]?.ToString() ?? string.Empty,
                        StartMode = obj["StartMode"]?.ToString() ?? string.Empty,
                        PathName = obj["PathName"]?.ToString() ?? string.Empty
                    });
                }
            }
#else
            using var searcher = new ManagementObjectSearcher("SELECT Name, State, StartMode, PathName FROM Win32_Service");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                context.Services.Add(new ServiceEntry
                {
                    Name = obj["Name"]?.ToString() ?? string.Empty,
                    State = obj["State"]?.ToString() ?? string.Empty,
                    StartMode = obj["StartMode"]?.ToString() ?? string.Empty,
                    PathName = obj["PathName"]?.ToString() ?? string.Empty
                });
            }
#endif
            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Queries WMI Win32_Service to build a list of all installed services and their states.
    // Returns CheckResult.Ok on success, appending to context.Services.
}
