using System.Text.Json.Serialization;

namespace VaxDrive.Models;

public sealed class Finding
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty; // CRITICAL|HIGH|MEDIUM|LOW

    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // EXPLOITABLE|PATCH_AVAILABLE|MITIGATED

    [JsonPropertyName("remediation_id")]
    public string? RemediationId { get; set; }
}
