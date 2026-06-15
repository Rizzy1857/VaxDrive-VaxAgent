using System;
using VaxDrive.Models;
using System.Threading;

namespace VaxDrive.VaxAgent.Checks;

public sealed class CveMatchCheck : ICheck
{
    private readonly DefinitionPack? _pack;

    public string Name => "CveMatchCheck";
    // Returns the static name of the check.

    public CveMatchCheck(DefinitionPack? pack)
    {
        _pack = pack;
    }
    // Initializes the check with the loaded definition pack.
    // Returns a new CveMatchCheck instance.

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        if (_pack == null || _pack.SoftwareCveRules.Count == 0)
        {
            return CheckResult.Ok();
        }

        try
        {
            foreach (SoftwareCveRule rule in _pack.SoftwareCveRules)
            {
                if (rule.Match.Type.Equals("installed_software", StringComparison.OrdinalIgnoreCase))
                {
                    CheckSoftwareRule(rule, context);
                }
                else if (rule.Match.Type.Equals("os_feature", StringComparison.OrdinalIgnoreCase))
                {
                    CheckOsFeatureRule(rule, context);
                }
                // missing_patch logic would go here
            }

            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Evaluates all definitions pack CVE rules against the accumulated scan context state.
    // Returns CheckResult.Ok on success, appending matched CVEs to context.Result.Findings.

    private void CheckSoftwareRule(SoftwareCveRule rule, ScanContext context)
    {
        string targetSoftware = rule.Match.SoftwareName ?? string.Empty;
        if (string.IsNullOrEmpty(targetSoftware)) return;

        foreach (SoftwareEntry software in context.InstalledSoftware)
        {
            if (software.DisplayName.Contains(targetSoftware, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseSemVer(software.DisplayVersion, out Version parsedVersion))
                {
                    Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | CveMatchCheck | Warning: Invalid installed version '{software.DisplayVersion}' for {targetSoftware}. Skipping.");
                    continue;
                }

                bool match = true;

                if (!string.IsNullOrEmpty(rule.Match.MinVersion))
                {
                    if (TryParseSemVer(rule.Match.MinVersion, out Version minVersion))
                    {
                        if (parsedVersion < minVersion) match = false;
                    }
                    else
                    {
                        Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | CveMatchCheck | Warning: Invalid min_version '{rule.Match.MinVersion}' in rule {rule.Id}. Skipping.");
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(rule.Match.MaxVersion))
                {
                    if (TryParseSemVer(rule.Match.MaxVersion, out Version maxVersion))
                    {
                        if (parsedVersion > maxVersion) match = false;
                    }
                    else
                    {
                        Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | CveMatchCheck | Warning: Invalid max_version '{rule.Match.MaxVersion}' in rule {rule.Id}. Skipping.");
                        continue;
                    }
                }

                if (match)
                {
                    context.Result.Findings.Add(new Finding
                    {
                        Id = rule.Id,
                        Severity = rule.Severity,
                        Component = software.DisplayName,
                        Status = rule.Status,
                        RemediationId = rule.RemediationId
                    });
                    break; // One hit per rule per device is enough
                }
            }
        }
    }
    // Compares a single software CVE rule against the context's InstalledSoftware list using SemVer boundaries.
    // Appends a new Finding if a match is detected. Returns void.

    internal static bool TryParseSemVer(string version, out Version parsed)
    {
        parsed = null;
        if (string.IsNullOrEmpty(version)) return false;

        int dashIndex = version.IndexOf('-');
        string cleanVersion = dashIndex > 0 ? version.Substring(0, dashIndex) : version;

        string[] parts = cleanVersion.Split('.');
        int major = 0, minor = 0, patch = 0;

        if (parts.Length > 0 && !int.TryParse(parts[0], out major)) return false;
        if (parts.Length > 1 && !int.TryParse(parts[1], out minor)) return false;
        if (parts.Length > 2 && !int.TryParse(parts[2], out patch)) return false;

        parsed = new Version(major, minor, patch);
        return true;
    }

    private void CheckOsFeatureRule(SoftwareCveRule rule, ScanContext context)
    {
        string targetFeature = rule.Match.Feature ?? string.Empty;
        if (string.IsNullOrEmpty(targetFeature)) return;

        if (targetFeature.Equals("SMBv1", StringComparison.OrdinalIgnoreCase))
        {
            foreach (ServiceEntry service in context.Services)
            {
                if (service.Name.Equals("mrxsmb10", StringComparison.OrdinalIgnoreCase) &&
                    service.State.Equals("Running", StringComparison.OrdinalIgnoreCase))
                {
                    context.Result.Findings.Add(new Finding
                    {
                        Id = rule.Id,
                        Severity = rule.Severity,
                        Component = targetFeature,
                        Status = rule.Status,
                        RemediationId = rule.RemediationId
                    });
                    break;
                }
            }
        }
    }
    // Specialized logic for OS-level feature checks like SMBv1 using the Services list.
    // Appends a new Finding if a match is detected. Returns void.
}
