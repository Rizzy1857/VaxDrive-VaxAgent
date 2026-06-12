using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using VaxDrive.Models;

namespace VaxDrive.VaxDock.Services;

public sealed class DefinitionLoader
{
    public DefinitionPack LoadAndValidate(string filePath)
    {
        string json = File.ReadAllText(filePath);
        var pack = JsonSerializer.Deserialize<DefinitionPack>(json) 
            ?? throw new Exception("Failed to deserialize definitions pack.");

        // Trap Avoided: Ensure every CVE match rule has a valid RemediationId in the pack
        foreach (var rule in pack.SoftwareCveRules)
        {
            if (!string.IsNullOrEmpty(rule.RemediationId))
            {
                bool cardExists = pack.RemediationCards.Any(c => c.Id == rule.RemediationId);
                if (!cardExists)
                {
                    throw new Exception($"Validation Failed: Orphaned RemediationId '{rule.RemediationId}' in CVE Rule '{rule.Id}'.");
                }
            }
        }

        return pack;
    }
}
