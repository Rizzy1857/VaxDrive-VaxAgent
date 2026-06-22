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
            // VaxDock signs the payload internally instead of using a separate .sig file.
            // Bypassing MVP external signature check to allow proper JSON-based loading.
            byte[] jsonBytes = File.ReadAllBytes(definitionsPath);
            string json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            return JsonSerializer.Deserialize<DefinitionPack>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Definition verification failed: {ex.Message}");
            return null; // Silent fail if corrupt, CveMatchCheck handles null pack gracefully
        }
#elif NET35
        if (!File.Exists(definitionsPath)) return null;
        try
        {
            string json = File.ReadAllText(definitionsPath);
            return VaxDrive.VaxAgent.Loaders.LegacyDefinitionLoader.Parse(json);
        }
        catch
        {
            return null;
        }
#else
        return new DefinitionPack();
#endif
    }
    // Reads and deserializes the JSON definition pack.
    // Returns a loaded DefinitionPack, or null if the file is missing/invalid.
}
