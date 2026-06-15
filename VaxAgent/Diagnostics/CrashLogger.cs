using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace VaxDrive.VaxAgent.Diagnostics;

public static class CrashLogger
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    public static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            if (GetConsoleWindow() == IntPtr.Zero)
            {
                WriteCrashLog(ex);
            }
        }
    }

    public static void WriteCrashLog(Exception ex)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string buildKey = Environment.GetEnvironmentVariable("VAXDRIVE_BUILD_KEY");
        bool hasKey = !string.IsNullOrEmpty(buildKey);

        string filename = hasKey ? $"crash_{timestamp}.log" : $"UNSIGNED_crash_{timestamp}.log";
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);

        var sb = new StringBuilder();
        sb.AppendLine($"--- VAXDRIVE FATAL CRASH ---");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
        sb.AppendLine($"Message: {ex.Message}");
        sb.AppendLine($"Stack Trace:");
        sb.AppendLine(ex.StackTrace);

        string content = sb.ToString();

        if (hasKey)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(buildKey!);
            byte[] dataBytes = Encoding.UTF8.GetBytes(content);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hash = hmac.ComputeHash(dataBytes);
                string sig = Convert.ToHexString(hash).ToLowerInvariant();
                content = $"[HMAC:{sig}]\r\n{content}";
            }
        }

        try
        {
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
        catch
        {
            // Failsafe: If we can't write to BaseDirectory, silently drop
        }
    }
}
