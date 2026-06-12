using System;
using System.Net.Sockets;
using VaxDrive.VaxAgent.Logging;

namespace VaxDrive.VaxAgent.Network;

public static class S7Scanner
{
    // COTP Connection Request
    private static readonly byte[] CotpCr = new byte[] {
        0x03, 0x00, 0x00, 0x16, // TPKT
        0x11, 0xE0, 0x00, 0x00, 0x00, 0x01, 0x00, 0xC0, 0x01, 0x0A, 0xC1, 0x02, 0x01, 0x00, 0xC2, 0x02, 0x01, 0x02
    };

    // S7 Setup Communication
    private static readonly byte[] S7Setup = new byte[] {
        0x03, 0x00, 0x00, 0x19, // TPKT
        0x02, 0xF0, 0x80,       // COTP Data
        0x32, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, // S7 Header
        0xF0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x03, 0xC0 // S7 Parameter
    };

    // S7 Read SZL ID 0x0011, Index 0x0000
    private static readonly byte[] S7ReadSzl = new byte[] {
        0x03, 0x00, 0x00, 0x21, // TPKT
        0x02, 0xF0, 0x80,       // COTP Data
        0x32, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x08, 0x00, // S7 Header
        0x04, 0x01, 0x12, 0x0A, 0x10, 0x02, 0x00, 0x11, 0x00, 0x00, // S7 Parameter SZL
        0x12, 0x04, 0x00, 0x01, 0x00, 0x00 // Data
    };

    public static string? Scan(string ip)
    {
        int bytesSent = 0;
        int bytesReceived = 0;

        try
        {
            // Trap Avoided: using block guarantees the TCP socket is closed preventing half-open SCADA block
            using (var client = new TcpClient())
            {
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 3000;
                
                var result = client.BeginConnect(ip, 102, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                if (!success) return null; // Timeout
                
                client.EndConnect(result);
                using (var stream = client.GetStream())
                {
                    // 1. COTP CR
                    stream.Write(CotpCr, 0, CotpCr.Length);
                    bytesSent += CotpCr.Length;
                    
                    byte[] buffer = new byte[1024];
                    int read = stream.Read(buffer, 0, buffer.Length);
                    bytesReceived += read;
                    if (read == 0) return null;

                    // 2. Setup
                    stream.Write(S7Setup, 0, S7Setup.Length);
                    bytesSent += S7Setup.Length;
                    read = stream.Read(buffer, 0, buffer.Length);
                    bytesReceived += read;
                    if (read == 0) return null;

                    // 3. Read SZL (Never a Write PDU)
                    stream.Write(S7ReadSzl, 0, S7ReadSzl.Length);
                    bytesSent += S7ReadSzl.Length;
                    read = stream.Read(buffer, 0, buffer.Length);
                    bytesReceived += read;
                    if (read == 0) return null;

                    return ParseBanner(buffer, read);
                }
            }
        }
        catch { }
        finally
        {
            // Log interaction
            if (bytesSent > 0)
                AuditLogger.LogTcpConnection(ip, 102, bytesSent, bytesReceived);
        }
        return null;
    }

    public static string? ParseBanner(byte[] buffer, int readLength)
    {
        // Crude parse for SZL 0x0011 response. Module ident is a 20-byte ASCII string at offset.
        if (readLength > 41)
        {
            char[] chars = new char[readLength];
            for(int i=0; i<readLength; i++) chars[i] = (buffer[i] >= 32 && buffer[i] <= 126) ? (char)buffer[i] : '.';
            return new string(chars).Trim('.');
        }
        return null;
    }
}
