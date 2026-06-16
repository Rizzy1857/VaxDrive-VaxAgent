using System;
using System.IO;
using System.Text.Json;

namespace VaxDrive.VaxDock.Services;

public class AppSettings
{
    public string NvdApiKey { get; set; } = string.Empty;
    public int CadenceThresholdDays { get; set; } = 7;
}

public static class SettingsManager
{
    private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }
}
