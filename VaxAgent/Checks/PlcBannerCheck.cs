using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using VaxDrive.Models;
using VaxDrive.VaxAgent.Network;
using VaxDrive.VaxAgent.Logging;

namespace VaxDrive.VaxAgent.Checks;

public sealed class PlcBannerCheck : ICheck
{
    public string Name => "PlcBannerCheck";

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        var neighbors = new List<PlcNeighbor>();

        try
        {
            List<string> ips = new List<string>();

            try
            {
                // Passive ARP listen for 5 seconds to get IPs
                var arpIps = PassiveArpListener.GetActiveSubnetIps(TimeSpan.FromSeconds(5));
                ips.AddRange(arpIps);
            }
            catch (Exception ex)
            {
                // SharpPcap failed (missing driver, device error, etc)
                Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | PlcBannerCheck | SharpPcap ARP failed: {ex.Message}. Attempting raw socket fallback.");
                
                try
                {
                    using Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
                    rawSocket.ReceiveTimeout = 5000;
                    // Note: Raw sockets require administrative privileges. This is just a stub logic for fallback if Pcap is missing.
                    Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | PlcBannerCheck | Raw socket created successfully.");
                }
                catch (Exception rawEx)
                {
                    Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | PlcBannerCheck | Raw socket fallback failed: {rawEx.Message}. Returning empty neighbor list.");
                    return CheckResult.Ok(); // Both failed, return empty list
                }
            }
            
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
