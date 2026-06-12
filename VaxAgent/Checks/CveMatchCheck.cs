using System;
using VaxDrive.Models;

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

    public CheckResult Run(ScanContext context)
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
                // In Phase 1, we do naive string match instead of full SemVer.
                // TODO: Implement SemVer parser for min_version / max_version constraints.
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
    // Compares a single software CVE rule against the context's InstalledSoftware list.
    // Appends a new Finding if a match is detected. Returns void.

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
