namespace VaxDrive.VaxAgent.Checks.Yara;

public class YaraMatch
{
    public string RuleName { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
