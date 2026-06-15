#if NET35
using System;
using System.Collections.Generic;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Loaders;

public static class LegacyDefinitionLoader
{
    public static DefinitionPack Parse(string jsonContent)
    {
        if (string.IsNullOrEmpty(jsonContent))
            return new DefinitionPack { Cves = new List<CveRecord>() };

        var records = new List<CveRecord>();
        
        // Remove brackets for the outer array if present
        int startBracket = jsonContent.IndexOf('[');
        int endBracket = jsonContent.LastIndexOf(']');
        if (startBracket >= 0 && endBracket > startBracket)
        {
            jsonContent = jsonContent.Substring(startBracket + 1, endBracket - startBracket - 1);
        }

        // We assume a flat array of objects separated by ',' but splitting by '}' 
        // works well to isolate each object.
        string[] objects = jsonContent.Split(new[] { '}' }, StringSplitOptions.RemoveEmptyEntries);
        
        int lineNumber = 1;

        foreach (string objStr in objects)
        {
            string cleanObj = objStr.Trim(' ', '\r', '\n', ',', '{');
            if (string.IsNullOrEmpty(cleanObj)) continue;

            string[] pairs = cleanObj.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            
            string? cveId = null;
            string? minVer = null;
            string? maxVer = null;
            double? severity = null;

            foreach (string pair in pairs)
            {
                int colonIdx = pair.IndexOf(':');
                if (colonIdx < 0) continue;

                string key = pair.Substring(0, colonIdx).Trim(' ', '"', '\r', '\n');
                string val = pair.Substring(colonIdx + 1).Trim(' ', '"', '\r', '\n');

                if (key.Equals("cve_id", StringComparison.OrdinalIgnoreCase))
                    cveId = val;
                else if (key.Equals("min_version", StringComparison.OrdinalIgnoreCase))
                    minVer = val;
                else if (key.Equals("max_version", StringComparison.OrdinalIgnoreCase))
                    maxVer = val;
                else if (key.Equals("severity", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                    {
                        severity = s;
                    }
                }
            }

            if (cveId == null || minVer == null || maxVer == null || severity == null)
            {
                throw new FormatException($"Invalid JSON format at object index/line {lineNumber}. Missing required fields (cve_id, min_version, max_version, severity).");
            }

            records.Add(new CveRecord
            {
                CveId = cveId,
                MinVersion = minVer,
                MaxVersion = maxVer,
                Severity = severity.Value
            });

            lineNumber++;
        }

        return new DefinitionPack { Cves = records };
    }
}
#endif
