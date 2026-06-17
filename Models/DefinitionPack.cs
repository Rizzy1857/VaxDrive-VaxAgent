using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VaxDrive.Models;

public sealed class DefinitionPack
{
    [JsonPropertyName("pack_version")]
    public string PackVersion { get; set; } = string.Empty;

    [JsonPropertyName("generated")]
    public string Generated { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("software_cve_rules")]
    public List<SoftwareCveRule> SoftwareCveRules { get; set; } = new List<SoftwareCveRule>();

    [JsonPropertyName("remediation_cards")]
    public List<RemediationCard> RemediationCards { get; set; } = new List<RemediationCard>();

    [JsonPropertyName("usb_allowlist")]
    public List<UsbAllowlistEntry> UsbAllowlist { get; set; } = new List<UsbAllowlistEntry>();

    [JsonPropertyName("process_iocs")]
    public List<ProcessIoc> ProcessIocs { get; set; } = new List<ProcessIoc>();
}

public sealed class SoftwareCveRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("remediation_id")]
    public string RemediationId { get; set; } = string.Empty;

    [JsonPropertyName("match")]
    public RuleMatch Match { get; set; } = new RuleMatch();
}

public sealed class RuleMatch
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // os_feature, missing_patch, installed_software

    [JsonPropertyName("feature")]
    public string? Feature { get; set; }

    [JsonPropertyName("kb")]
    public string? Kb { get; set; }

    [JsonPropertyName("software_name")]
    public string? SoftwareName { get; set; }

    [JsonPropertyName("min_version")]
    public string? MinVersion { get; set; }

    [JsonPropertyName("max_version")]
    public string? MaxVersion { get; set; }
}

public sealed class RemediationCard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // PatchAvailable, VendorAdvisory, IsolationRecommended, UnsupportedSoftware
}

public sealed class UsbAllowlistEntry
{
    [JsonPropertyName("vid")]
    public string Vid { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public sealed class ProcessIoc
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;
}
