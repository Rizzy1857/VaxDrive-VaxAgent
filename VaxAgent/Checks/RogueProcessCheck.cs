using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Checks;

public sealed class RogueProcessCheck : ICheck
{
    private readonly DefinitionPack? _pack;

    public string Name => "RogueProcessCheck";
    // Returns the static name of the check.

    public RogueProcessCheck(DefinitionPack? pack)
    {
        _pack = pack;
    }
    // Initializes the check with the loaded definition pack.
    // Returns a new RogueProcessCheck instance.

    public CheckResult Run(ScanContext context, CancellationToken ct)
    {
        if (_pack == null || _pack.ProcessIocs.Count == 0)
        {
            return CheckResult.Ok(); // Nothing to check against
        }

        try
        {
            var iocNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ProcessIoc ioc in _pack.ProcessIocs)
            {
                iocNames.Add(ioc.Name);
            }

#if NET35
            using (var searcher = new ManagementObjectSearcher("SELECT Name, ExecutablePath FROM Win32_Process"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? string.Empty;
                    if (iocNames.Contains(name))
                    {
                        context.Result.Findings.Add(new Finding
                        {
                            Id = $"IOC-PROC-{name.ToUpperInvariant()}",
                            Severity = "HIGH", // Simplified severity mapping
                            Component = name,
                            Status = "EXPLOITABLE",
                            RemediationId = null
                        });
                    }
                }
            }
#else
            using var searcher = new ManagementObjectSearcher("SELECT Name, ExecutablePath FROM Win32_Process");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? string.Empty;
                if (iocNames.Contains(name))
                {
                    context.Result.Findings.Add(new Finding
                    {
                        Id = $"IOC-PROC-{name.ToUpperInvariant()}",
                        Severity = "HIGH",
                        Component = name,
                        Status = "EXPLOITABLE",
                        RemediationId = null
                    });
                }
            }
#endif
            return CheckResult.Ok();
        }
        catch (Exception ex)
        {
            return CheckResult.Failed(ex.Message);
        }
    }
    // Queries Win32_Process to compare running process names against the definitions pack IOC list.
    // Returns CheckResult.Ok on success, appending findings to context.Result.Findings.
}
