using System.Text.Json.Serialization;

namespace VaxDrive.Models;

public sealed class FirmwareRecord
{
    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("smbios_bios_version")]
    public string SMBIOSBIOSVersion { get; set; } = string.Empty;

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = string.Empty;

    [JsonPropertyName("serial_number")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("confidence_score")]
    public double ConfidenceScore { get; set; }
}
