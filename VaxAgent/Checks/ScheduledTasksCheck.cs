using System;
using System.Diagnostics;
using System.IO;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class ScheduledTasksCheck : ICheck
{
    public string Name => "ScheduledTasksCheck";
    // Returns the static name of the check.

    public CheckResult Run(ScanContext context)
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

            string[] headers = ParseCsvLine(headerLine);
            int taskNameIdx = Array.IndexOf(headers, "TaskName");
            int taskToRunIdx = Array.IndexOf(headers, "Task To Run");
            int runAsUserIdx = Array.IndexOf(headers, "Run As User");

            if (taskNameIdx == -1) return CheckResult.Failed("Could not parse schtasks CSV header (TaskName missing).");

            string? line;
            while ((line = stringReader.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;
                
                string[] parts = ParseCsvLine(line);
                
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

    private string[] ParseCsvLine(string line)
    {
        var result = new System.Collections.Generic.List<string>();
        bool inQuotes = false;
        string current = "";
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }
        result.Add(current);
        return result.ToArray();
    }
    // Parses a single line of CSV text considering quote encapsulation.
    // Returns an array of string fields extracted from the CSV line.
}
