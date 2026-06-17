using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VaxDrive.Models;

public sealed class ScanResult
{
    [JsonPropertyName("scan_id")]
    public string ScanId { get; set; } = Guid.NewGuid().ToString("D");

    [JsonPropertyName("device_fingerprint")]
    public string DeviceFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("definitions_pack_version")]
    public string DefinitionsPackVersion { get; set; } = "1.0.0";

    [JsonPropertyName("definitions_pack_generated")]
    public string DefinitionsPackGenerated { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("os")]
    public string Os { get; set; } = string.Empty;

    [JsonPropertyName("firmware")]
    public FirmwareRecord Firmware { get; set; } = new FirmwareRecord();

    [JsonPropertyName("patch_level")]
    public string? PatchLevel { get; set; }

    [JsonPropertyName("findings")]
    public List<Finding> Findings { get; set; } = new List<Finding>();

    [JsonPropertyName("usb_anomalies")]
    public List<string> UsbAnomalies { get; set; } = new List<string>();

    [JsonPropertyName("open_ports")]
    public List<int> OpenPorts { get; set; } = new List<int>();

    [JsonPropertyName("plc_neighbors")]
    public List<PlcNeighbor> PlcNeighbors { get; set; } = new List<PlcNeighbor>();

    [JsonPropertyName("scan_completeness")]
    public string ScanCompleteness { get; set; } = "100%";

    [JsonPropertyName("check_errors")]
    public Dictionary<string, string> CheckErrors { get; set; } = new Dictionary<string, string>();
}
