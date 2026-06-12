using System;
using System.Diagnostics;
using System.IO;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class OpenPortsCheck : ICheck
{
    public string Name => "OpenPortsCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context)
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

            using StringReader stringReader = new StringReader(output);
            string? line;
            while ((line = stringReader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Format:  Proto  Local Address          Foreign Address        State           PID
                // Example: TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       1124
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Ensure we have enough columns and the state is LISTENING
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

            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Executes netstat -ano, parses output to find LISTENING TCP ports.
    // Returns CheckResult.Ok on success, appending distinct ints to context.Result.OpenPorts.
}
