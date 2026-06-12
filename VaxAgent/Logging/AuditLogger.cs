using System;
using System.IO;

namespace VaxDrive.VaxAgent.Logging;

public static class AuditLogger
{
    private static readonly string LogDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "logs"));

    static AuditLogger()
    {
        if (!Directory.Exists(LogDir))
        {
            try { Directory.CreateDirectory(LogDir); } catch { }
        }
    }

    public static void LogTcpConnection(string targetIp, int port, int bytesSent, int bytesReceived)
    {
        try
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string logLine = $"{timestamp} | {targetIp} | {port} | {bytesSent} | {bytesReceived}\n";
            string logFile = Path.Combine(LogDir, "audit.log");
            File.AppendAllText(logFile, logLine);
        }
        catch { /* Fallback or ignore if write-protected */ }
    }
    
    public static void LogEvent(string eventType, string message)
    {
        try
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string logLine = $"{timestamp} | {eventType} | {message}\n";
            string logFile = Path.Combine(LogDir, "audit.log");
            File.AppendAllText(logFile, logLine);
        }
        catch { }
    }
}
