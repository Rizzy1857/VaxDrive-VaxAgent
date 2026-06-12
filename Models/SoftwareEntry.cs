namespace VaxDrive.Models;

public sealed class SoftwareEntry
{
    public string DisplayName { get; init; } = string.Empty;
    public string DisplayVersion { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string InstallDate { get; init; } = string.Empty;
}
