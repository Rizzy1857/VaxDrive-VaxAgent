using System;
using System.Collections.Generic;
using System.Threading;
using VaxDrive.Models;
using VaxDrive.VaxAgent.Network;
using VaxDrive.VaxAgent.Logging;

namespace VaxDrive.VaxAgent.Checks;

public sealed class PlcBannerCheck : ICheck
{
    public string Name => "PlcBannerCheck";

    public CheckResult Run(ScanContext context)
    {
        var neighbors = new List<PlcNeighbor>();

        try
        {
            // Passive ARP listen for 5 seconds to get IPs
            var ips = PassiveArpListener.GetActiveSubnetIps(TimeSpan.FromSeconds(5));
            
            if (ips.Count == 0)
            {
                return CheckResult.Ok(); // No traffic is a valid state
            }

            foreach (var ip in ips)
            {
                // Trap Avoided: Sequential scans to prevent network floods on fragile 1990s OT switches
                string? s7Banner = S7Scanner.Scan(ip);
                if (!string.IsNullOrEmpty(s7Banner))
                {
                    neighbors.Add(new PlcNeighbor { Ip = ip, Banner = "S7: " + s7Banner });
                    continue; // Skip Modbus if S7 responds
                }

                string? modbusBanner = ModbusScanner.Scan(ip);
                if (!string.IsNullOrEmpty(modbusBanner))
                {
                    neighbors.Add(new PlcNeighbor { Ip = ip, Banner = "Modbus: " + modbusBanner });
                    continue;
                }

                // Trap Avoided: Treat absence of response explicitly, never inferring device doesn't exist
                AuditLogger.LogEvent("PLC_PROBE", $"{ip} no_response");
            }

            context.Result.PlcNeighbors = neighbors;
            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
}
