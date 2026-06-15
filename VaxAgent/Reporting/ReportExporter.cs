using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VaxDrive.VaxAgent.Checks.Yara;
using VaxDrive.VaxAgent.Network;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxAgent.Reporting;

public class ReportExporter
{
    private readonly string _buildKey;
    private readonly string _outputDirectory;

    public ReportExporter()
    {
        _buildKey = Environment.GetEnvironmentVariable("VAXDRIVE_BUILD_KEY") ?? string.Empty;
        
        // Output to VaxDrive root, fallback to current dir if not set
        _outputDirectory = Environment.GetEnvironmentVariable("VAXDRIVE_ROOT") ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    public async Task ExportReportAsync(TopologyMap topology, List<YaraMatch> yaraHits, List<DeltaRecord> deltas)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string htmlFilename = $"report_{timestamp}.html";
        string jsonFilename = $"report_{timestamp}.json";

        string htmlPath = Path.Combine(_outputDirectory, htmlFilename);
        string jsonPath = Path.Combine(_outputDirectory, jsonFilename);

        string htmlContent = GenerateHtml(topology, yaraHits, deltas, timestamp);
        string jsonContent = GenerateJson(topology, yaraHits, deltas);

        await File.WriteAllTextAsync(htmlPath, htmlContent, Encoding.UTF8).ConfigureAwait(false);
        await File.WriteAllTextAsync(jsonPath, jsonContent, Encoding.UTF8).ConfigureAwait(false);

        WriteSignatureFile(htmlPath, htmlContent);
        WriteSignatureFile(jsonPath, jsonContent);
    }

    private string GenerateHtml(TopologyMap topology, List<YaraMatch> yaraHits, List<DeltaRecord> deltas, string timestamp)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><title>VaxDrive Agent Report</title>");
        sb.AppendLine("<style>body { font-family: sans-serif; background: #0B132B; color: #fff; } table { width: 100%; border-collapse: collapse; } th, td { border: 1px solid #444; padding: 8px; text-align: left; } th { background-color: #1a2a5c; }</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>VaxDrive Agent Report</h1>");
        sb.AppendLine($"<p><strong>Version:</strong> {version}</p>");
        sb.AppendLine($"<p><strong>Generated (UTC):</strong> {timestamp}</p>");

        // Topology Table
        sb.AppendLine("<h2>Topology Discovered</h2>");
        sb.AppendLine("<table><tr><th>MAC</th><th>IP</th><th>Protocol</th><th>Vendor</th></tr>");
        foreach (var asset in topology.GetAssets())
        {
            sb.AppendLine($"<tr><td>{asset.MacAddress}</td><td>{asset.IpAddress}</td><td>{asset.Protocol}</td><td>{asset.Vendor}</td></tr>");
        }
        sb.AppendLine("</table>");

        // YARA Table
        sb.AppendLine("<h2>YARA Matches</h2>");
        sb.AppendLine("<table><tr><th>Rule Name</th><th>Target</th><th>Severity</th></tr>");
        foreach (var hit in yaraHits)
        {
            sb.AppendLine($"<tr><td>{hit.RuleName}</td><td>{hit.Target}</td><td>{hit.Severity}</td></tr>");
        }
        sb.AppendLine("</table>");

        // CVE Deltas Table
        sb.AppendLine("<h2>CVE Severity Deltas</h2>");
        sb.AppendLine("<table><tr><th>CVE ID</th><th>Old Score</th><th>New Score</th><th>Recorded At</th></tr>");
        foreach (var delta in deltas)
        {
            sb.AppendLine($"<tr><td>{delta.CveId}</td><td>{delta.OldScore}</td><td>{delta.NewScore}</td><td>{delta.RecordedAt}</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string GenerateJson(TopologyMap topology, List<YaraMatch> yaraHits, List<DeltaRecord> deltas)
    {
        var data = new
        {
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            Topology = topology.GetAssets(),
            YaraHits = yaraHits,
            CveDeltas = deltas
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    private void WriteSignatureFile(string targetPath, string content)
    {
        if (string.IsNullOrEmpty(_buildKey)) return;

        byte[] keyBytes = Encoding.UTF8.GetBytes(_buildKey);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);

        using var hmac = new HMACSHA256(keyBytes);
        byte[] hash = hmac.ComputeHash(contentBytes);
        string sigHex = Convert.ToHexString(hash).ToLowerInvariant();

        string sigPath = targetPath + ".sig";
        File.WriteAllText(sigPath, sigHex, Encoding.UTF8);
    }
}
