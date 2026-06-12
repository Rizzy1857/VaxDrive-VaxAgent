using System.Collections.Generic;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent;

public sealed class ScanContext
{
    public ScanResult Result { get; } = new ScanResult();
    public string DefinitionsPath { get; init; } = string.Empty;
    public string ResultsPath { get; init; } = string.Empty;
    public List<SoftwareEntry> InstalledSoftware { get; } = new List<SoftwareEntry>();
    public List<ServiceEntry> Services { get; } = new List<ServiceEntry>();
    public List<ScheduledTaskEntry> ScheduledTasks { get; } = new List<ScheduledTaskEntry>();
}
