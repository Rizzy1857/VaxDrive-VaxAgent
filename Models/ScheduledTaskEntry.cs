namespace VaxDrive.Models;

public sealed class ScheduledTaskEntry
{
    public string TaskName { get; init; } = string.Empty;
    public string RunAsUser { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
}
