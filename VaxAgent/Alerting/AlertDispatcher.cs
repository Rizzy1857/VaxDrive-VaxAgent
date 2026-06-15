using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VaxDrive.VaxAgent.Alerting;

public class AlertDispatcher : IDisposable
{
    private readonly string _syslogHost;
    private readonly int _syslogPort;
    private readonly string _flatFilePath;
    private readonly string _buildKey;
    private readonly string _version;

    private readonly ConcurrentQueue<FailedAlert> _retryQueue;
    private readonly CancellationTokenSource _cts;
    private readonly Task _retryTask;

    private class FailedAlert
    {
        public string Payload { get; set; } = "";
        public int Attempts { get; set; } = 0;
    }

    public AlertDispatcher()
    {
        _syslogHost = Environment.GetEnvironmentVariable("VAXDRIVE_SYSLOG_HOST") ?? "127.0.0.1";
        string portStr = Environment.GetEnvironmentVariable("VAXDRIVE_SYSLOG_PORT") ?? "514";
        if (!int.TryParse(portStr, out _syslogPort)) _syslogPort = 514;

        _flatFilePath = Environment.GetEnvironmentVariable("VAXDRIVE_ALERT_LOG_PATH") ?? string.Empty;
        _buildKey = Environment.GetEnvironmentVariable("VAXDRIVE_BUILD_KEY") ?? string.Empty;

        _version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        _retryQueue = new ConcurrentQueue<FailedAlert>();
        _cts = new CancellationTokenSource();
        _retryTask = Task.Run(() => RetryLoopAsync(_cts.Token));
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _retryTask.Wait(1000); } catch { }
        _cts.Dispose();
    }

    public async Task DispatchAlertAsync(int eventId, string name, int severity, string extensions)
    {
        string cefPayload = FormatCef(eventId, name, severity, extensions);

        await Task.WhenAll(
            SendToSyslogAsync(cefPayload, false),
            WriteToEventLogAsync(eventId, name, severity, cefPayload),
            WriteToFlatFileAsync(cefPayload, false)
        ).ConfigureAwait(false);
    }

    public string FormatCef(int eventId, string name, int severity, string extensions)
    {
        return $"CEF:0|VaxDrive|VaxAgent|{_version}|{eventId}|{name}|{severity}|{extensions}";
    }

    // internal for testing
    internal async Task SendToSyslogAsync(string payload, bool isRetry)
    {
        if (string.IsNullOrEmpty(_syslogHost) || _syslogHost == "DISABLED") return;

        try
        {
            using var udpClient = new UdpClient();
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await udpClient.SendAsync(bytes, bytes.Length, _syslogHost, _syslogPort).ConfigureAwait(false);
        }
        catch
        {
            if (!isRetry)
            {
                _retryQueue.Enqueue(new FailedAlert { Payload = payload, Attempts = 1 });
            }
            else
            {
                throw;
            }
        }
    }

    private async Task RetryLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            int count = _retryQueue.Count;
            for (int i = 0; i < count; i++)
            {
                if (_retryQueue.TryDequeue(out var alert))
                {
                    try
                    {
                        await SendToSyslogAsync(alert.Payload, true).ConfigureAwait(false);
                    }
                    catch
                    {
                        alert.Attempts++;
                        if (alert.Attempts >= 3)
                        {
                            await WriteToFlatFileAsync(alert.Payload, true).ConfigureAwait(false);
                        }
                        else
                        {
                            _retryQueue.Enqueue(alert);
                        }
                    }
                }
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private Task WriteToEventLogAsync(int eventId, string name, int severity, string payload)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                string source = "VaxDrive";
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

    private async Task WriteToFlatFileAsync(string payload, bool force)
    {
        string dirPath = _flatFilePath;
        
        if (string.IsNullOrEmpty(dirPath))
        {
            if (!force) return;
            // Fallback for forced writes (e.g. after 3 failed UDP attempts)
            dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        }

        try
        {
            string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            string filename = $"alerts_{dateStr}.log";
            string fullPath = Path.Combine(dirPath, filename);

            string hmacHex = "";
            if (!string.IsNullOrEmpty(_buildKey))
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_buildKey));
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                hmacHex = Convert.ToHexString(hash).ToLowerInvariant();
            }

            string logLine = $"[{DateTime.UtcNow:O}] [HMAC:{hmacHex}] {payload}{Environment.NewLine}";

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            using var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, true);
            byte[] bytes = Encoding.UTF8.GetBytes(logLine);
            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }
        catch
        {
            // Silently drop
        }
    }
}
