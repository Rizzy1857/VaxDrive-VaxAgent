using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class OpenPortsCheck : ICheck
{
    public string Name => "OpenPortsCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat.exe",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process == null) return CheckResult.Failed("netstat.exe failed to start.");

            using StreamReader reader = process.StandardOutput;
            string output = reader.ReadToEnd();
            process.WaitForExit();

            ParseNetstatOutput(output, context);

            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Executes netstat -ano, parses output to find LISTENING TCP ports.
    // Returns CheckResult.Ok on success, appending distinct ints to context.Result.OpenPorts.

    internal static void ParseNetstatOutput(string output, ScanContext context)
    {
        bool regexMatchedAny = false;
        // Pattern: PROTO  LOCAL_IP:LOCAL_PORT  FOREIGN_IP:FOREIGN_PORT  STATE  PID
        var regex = new System.Text.RegularExpressions.Regex(@"^\s*TCP\s+([\d.\[\]:]+):(\d+)\s+([\d.\[\]:]+):(\d+)\s+\S.*?(\d+)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
        
        var matches = regex.Matches(output);
        if (matches.Count > 0)
        {
            regexMatchedAny = true;
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string foreignIp = match.Groups[3].Value;
                string foreignPort = match.Groups[4].Value;
                
                if ((foreignIp == "0.0.0.0" || foreignIp == "[::]") && foreignPort == "0")
                {
                    if (int.TryParse(match.Groups[2].Value, out int port))
                    {
                        if (!context.Result.OpenPorts.Contains(port))
                        {
                            context.Result.OpenPorts.Add(port);
                        }
                    }
                }
            }
        }

        if (!regexMatchedAny)
        {
            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | OpenPortsCheck | Regex yielded 0 results. Falling back to original parsing approach.");
            using StringReader stringReader = new StringReader(output);
            string? line;
            while ((line = stringReader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length >= 4 && parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
                {
                    string localAddress = parts[1];
                    int colonIndex = localAddress.LastIndexOf(':');
                    
                    if (colonIndex > 0 && colonIndex < localAddress.Length - 1)
                    {
                        string portStr = localAddress.Substring(colonIndex + 1);
                        if (int.TryParse(portStr, out int port))
                        {
                            if (!context.Result.OpenPorts.Contains(port))
                            {
                                context.Result.OpenPorts.Add(port);
                            }
                        }
                    }
                }
            }
        }
    }
}
