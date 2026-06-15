using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class ScheduledTasksCheck : ICheck
{
    public string Name => "Scheduled Tasks Check";
    public string Description => "Enumerates scheduled tasks for persistence mechanisms";

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = "/query /fo CSV /v",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return CheckResult.Failed("schtasks.exe failed to start.");
            }

            using StreamReader reader = process.StandardOutput;
            string output = reader.ReadToEnd();
            process.WaitForExit();

            using StringReader stringReader = new StringReader(output);
            
            // Dynamically parse headers because XP / 7 / 10 have different column orders
            string? headerLine = stringReader.ReadLine();
            if (string.IsNullOrEmpty(headerLine)) return CheckResult.Failed("No output from schtasks.");

            string[]? headers = ParseCsvLine(headerLine);
            if (headers == null) return CheckResult.Failed("Could not parse schtasks CSV header (Malformed).");

            int taskNameIdx = Array.IndexOf(headers, "TaskName");
            int taskToRunIdx = Array.IndexOf(headers, "Task To Run");
            int runAsUserIdx = Array.IndexOf(headers, "Run As User");

            if (taskNameIdx == -1) return CheckResult.Failed("Could not parse schtasks CSV header (TaskName missing).");

            string? line;
            while ((line = stringReader.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;
                
                string[]? parts = ParseCsvLine(line);
                if (parts == null) continue;
                
                string taskName = taskNameIdx >= 0 && taskNameIdx < parts.Length ? parts[taskNameIdx] : string.Empty;
                string command = taskToRunIdx >= 0 && taskToRunIdx < parts.Length ? parts[taskToRunIdx] : string.Empty;
                string runAs = runAsUserIdx >= 0 && runAsUserIdx < parts.Length ? parts[runAsUserIdx] : string.Empty;

                // Ignore header repetitions in multi-table output
                if (taskName == "TaskName") continue;

                context.ScheduledTasks.Add(new ScheduledTaskEntry
                {
                    TaskName = taskName,
                    Command = command,
                    RunAsUser = runAs
                });
            }

            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Executes schtasks.exe and parses its CSV output dynamically to gather scheduled task definitions.
    // Returns CheckResult.Ok on success, appending to context.ScheduledTasks.

    internal static string[]? ParseCsvLine(string line)
    {
        var result = new System.Collections.Generic.List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    result.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }
        }
        
        if (inQuotes)
        {
            Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | ScheduledTasksCheck | Warning: Malformed CSV row, unclosed quote. Skipping row.");
            return null;
        }
        
        result.Add(current.ToString());
        return result.ToArray();
    }
    // Parses a single line of CSV text considering RFC 4180 rules.
    // Returns an array of string fields, or null if malformed.
}
