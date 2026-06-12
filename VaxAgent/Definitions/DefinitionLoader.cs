using System;
using System.IO;
#if NET8_0
using System.Text.Json;
#endif
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Definitions;

public sealed class DefinitionLoader
{
    public DefinitionPack? Load(string definitionsPath)
    {
#if NET8_0
        if (!File.Exists(definitionsPath)) return null;

        try
        {
            // TODO: In Phase 2, verify HMAC signature of the pack before deserializing!
            string json = File.ReadAllText(definitionsPath);
            return JsonSerializer.Deserialize<DefinitionPack>(json);
        }
        catch
        {
            return null; // Silent fail if corrupt, CveMatchCheck handles null pack gracefully
        }
#else
        // TODO: .NET 3.5 has no System.Text.Json. We either need a micro-parser or DataContractJsonSerializer.
        // For now, return a blank pack so the legacy build compiles and runs without crashing.
        return new DefinitionPack();
#endif
    }
    // Reads and deserializes the JSON definition pack.
    // Returns a loaded DefinitionPack, or null if the file is missing/invalid.
}
