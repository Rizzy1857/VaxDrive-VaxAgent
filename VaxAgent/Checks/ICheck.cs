using System.Threading;

namespace VaxDrive.VaxAgent.Checks;

public interface ICheck
{
    string Name { get; }
    
    CheckResult Run(ScanContext context, CancellationToken ct);
    // Runs the specific security check and populates findings directly into context.Result
    // Returns CheckResult.Ok() if successful, or CheckResult.Failed(error) if unhandled exception occurs
}
