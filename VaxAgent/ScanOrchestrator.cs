using System;
using System.Collections.Generic;
using System.Diagnostics;
using VaxDrive.Models;
using VaxDrive.VaxAgent.Checks;

namespace VaxDrive.VaxAgent;

public sealed class ScanOrchestrator
{
    private readonly IEnumerable<ICheck> _checks;
    private readonly TimeSpan _budget = TimeSpan.FromSeconds(90);

    public ScanOrchestrator(IEnumerable<ICheck> checks)
    {
        _checks = checks;
    }
    // Initializes the orchestrator with an injected collection of checks to run.
    // Returns a new ScanOrchestrator instance.

    public ScanResult RunAll(ScanContext context)
    {
        Stopwatch sw = Stopwatch.StartNew();

        foreach (ICheck check in _checks)
        {
            if (sw.Elapsed > _budget)
            {
                context.Result.CheckErrors["Orchestrator"] = "Scan aborted: exceeded 90 second budget.";
                break;
            }

            try
            {
                CheckResult result = check.Run(context);
                if (!result.Success)
                {
                    context.Result.CheckErrors[check.Name] = result.Error ?? "Unknown error";
                }
            }
            catch (Exception ex)
            {
                // Double safety net: catching exceptions that escaped the check module's internal try/catch
                context.Result.CheckErrors[check.Name] = $"Unhandled exception: {ex.Message}";
            }
        }

        sw.Stop();
        return context.Result;
    }
    // Sequentially runs all injected checks against the context while enforcing a 90-second execution budget.
    // Returns the populated ScanResult from the context.
}
