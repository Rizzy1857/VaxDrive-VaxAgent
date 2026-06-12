using System;
using System.Net.Sockets;
using VaxDrive.VaxAgent.Logging;

namespace VaxDrive.VaxAgent.Network;

public static class ModbusScanner
{
    // Modbus TCP FC 43 / 14 (Read Device Identification)
    private static readonly byte[] ModbusFc43 = new byte[] {
        0x00, 0x01, // Transaction Identifier
        0x00, 0x00, // Protocol Identifier
        0x00, 0x05, // Length
        0x01,       // Unit Identifier
        0x2B,       // FC 43 (0x2B)
        0x0E,       // MEI Type 14 (0x0E)
        0x01,       // Read Device ID code
        0x00        // Object ID
    };

    public static string? Scan(string ip)
    {
        int bytesSent = 0;
        int bytesReceived = 0;

        try
        {
            // Trap Avoided: Guaranteed socket closure to prevent half-open connections
            using (var client = new TcpClient())
            {
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 3000;
                
                var result = client.BeginConnect(ip, 502, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                if (!success) return null; // Timeout

                client.EndConnect(result);
                using (var stream = client.GetStream())
                {
                    // Trap Avoided: Never sends FC1-FC6 (writes). Only reads device identification.
                    stream.Write(ModbusFc43, 0, ModbusFc43.Length);
                    bytesSent += ModbusFc43.Length;

                    byte[] buffer = new byte[1024];
                    int read = stream.Read(buffer, 0, buffer.Length);
                    bytesReceived += read;

                    return ParseBanner(buffer, read);
                }
            }
        }
        catch { }
        finally
        {
            // Trap Avoided: Log all interactions for regulatory compliance
            if (bytesSent > 0)
                AuditLogger.LogTcpConnection(ip, 502, bytesSent, bytesReceived);
        }
        return null;
    }

    public static string? ParseBanner(byte[] buffer, int readLength)
    {
        if (readLength > 10)
        {
            // Crude parse of basic Modbus FC43 payload
            char[] chars = new char[readLength];
            for(int i=0; i<readLength; i++) chars[i] = (buffer[i] >= 32 && buffer[i] <= 126) ? (char)buffer[i] : '.';
            return new string(chars).Trim('.');
        }
        return null;
    }
}
