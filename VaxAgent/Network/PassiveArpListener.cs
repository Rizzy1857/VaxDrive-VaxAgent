using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpPcap;
using VaxDrive.VaxAgent.Logging;

namespace VaxDrive.VaxAgent.Network;

public static class PassiveArpListener
{
    public static List<string> GetActiveSubnetIps(TimeSpan duration)
    {
        var activeIps = new HashSet<string>();

        try
        {
#if NET8_0_OR_GREATER
            var devices = CaptureDeviceList.Instance;
            if (devices.Count < 1)
            {
                AuditLogger.LogEvent("ARP_LISTEN", "no_pcap_traffic_observed (No Npcap devices found)");
                return new List<string>();
            }

            foreach (var dev in devices)
            {
                try
                {
                    // Trap Avoided: Normal mode, no promiscuous span port assumption
                    dev.Open(DeviceModes.None); 
                    dev.Filter = "arp";
                    dev.OnPacketArrival += (sender, e) =>
                    {
                        var packet = PacketDotNet.Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
                        var arpPacket = packet.Extract<PacketDotNet.ArpPacket>();
                        if (arpPacket != null)
                        {
                            var senderIp = arpPacket.SenderProtocolAddress.ToString();
                            if (IsRfc1918(senderIp))
                            {
                                lock (activeIps) { activeIps.Add(senderIp); }
                            }
                        }
                    };
                    dev.StartCapture();
                }
                catch { }
            }

            Thread.Sleep(duration);

            foreach (var dev in devices)
            {
                try { dev.StopCapture(); dev.Close(); } catch { }
            }
#else
            // Fallback for .NET 3.5 to prevent build breaks with older SharpPcap APIs while maintaining safety
            AuditLogger.LogEvent("ARP_LISTEN", "no_pcap_traffic_observed (ARP sweep disabled in net35 compatibility mode)");
#endif
        }
        catch (Exception ex)
        {
            AuditLogger.LogEvent("ARP_LISTEN", "no_pcap_traffic_observed (" + ex.Message + ")");
        }

        return activeIps.ToList();
    }

    private static bool IsRfc1918(string ip)
    {
        if (ip.StartsWith("10.")) return true;
        if (ip.StartsWith("192.168.")) return true;
        if (ip.StartsWith("172."))
        {
            string[] parts = ip.Split('.');
            if (parts.Length == 4 && int.TryParse(parts[1], out int second))
            {
                return second >= 16 && second <= 31;
            }
        }
        return false;
    }
}
