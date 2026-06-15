using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace VaxDrive.VaxAgent.Alerting;

public class AlertDispatcher
{
    private readonly string _syslogHost;
    private readonly int _syslogPort;
    private readonly string _flatFilePath;
    private readonly string _buildKey;
    private readonly string _version;

    public AlertDispatcher()
    {
        _syslogHost = Environment.GetEnvironmentVariable("VAXDRIVE_SYSLOG_HOST") ?? "127.0.0.1";
        string portStr = Environment.GetEnvironmentVariable("VAXDRIVE_SYSLOG_PORT") ?? "514";
        if (!int.TryParse(portStr, out _syslogPort)) _syslogPort = 514;

        _flatFilePath = Environment.GetEnvironmentVariable("VAXDRIVE_ALERT_LOG_PATH") ?? string.Empty;
        _buildKey = Environment.GetEnvironmentVariable("VAXDRIVE_BUILD_KEY") ?? string.Empty;

        _version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }

    public async Task DispatchAlertAsync(int eventId, string name, int severity, string extensions)
    {
        string cefPayload = FormatCef(eventId, name, severity, extensions);

        await Task.WhenAll(
            SendToSyslogAsync(cefPayload),
            WriteToEventLogAsync(eventId, name, severity, cefPayload),
            WriteToFlatFileAsync(cefPayload)
        ).ConfigureAwait(false);
    }

    public string FormatCef(int eventId, string name, int severity, string extensions)
    {
        return $"CEF:0|VaxDrive|VaxAgent|{_version}|{eventId}|{name}|{severity}|{extensions}";
    }

    private async Task SendToSyslogAsync(string payload)
    {
        // Skip if Syslog host is empty or dummy value
        if (string.IsNullOrEmpty(_syslogHost) || _syslogHost == "DISABLED") return;

        try
        {
            using var udpClient = new UdpClient();
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await udpClient.SendAsync(bytes, bytes.Length, _syslogHost, _syslogPort).ConfigureAwait(false);
        }
        catch
        {
            // Silently drop if network is unavailable to prevent agent crash
        }
    }

    private Task WriteToEventLogAsync(int eventId, string name, int severity, string payload)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                string source = "VaxDrive";
                // Skip if not registered/admin rights, EventLog.SourceExists can throw so catch it
                try
                {
                    if (!EventLog.SourceExists(source)) return Task.CompletedTask;

                    EventLogEntryType type = severity >= 7 ? EventLogEntryType.Error :
                                             severity >= 4 ? EventLogEntryType.Warning :
                                             EventLogEntryType.Information;

                    EventLog.WriteEntry(source, payload, type, eventId);
                }
                catch { }
            }
        }
        catch { }
        
        return Task.CompletedTask;
    }

    private async Task WriteToFlatFileAsync(string payload)
    {
        if (string.IsNullOrEmpty(_flatFilePath)) return;

        try
        {
            string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            string filename = $"alerts_{dateStr}.log";
            string fullPath = Path.Combine(_flatFilePath, filename);

            string hmacHex = "";
            if (!string.IsNullOrEmpty(_buildKey))
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_buildKey));
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                hmacHex = Convert.ToHexString(hash).ToLowerInvariant();
            }

            string logLine = $"[{DateTime.UtcNow:O}] [HMAC:{hmacHex}] {payload}{Environment.NewLine}";

            // Ensure directory exists
            string? dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Using FileShare.ReadWrite for concurrent append safety
            using var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, true);
            byte[] bytes = Encoding.UTF8.GetBytes(logLine);
            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }
        catch
        {
            // Silently drop file write errors
        }
    }
}
