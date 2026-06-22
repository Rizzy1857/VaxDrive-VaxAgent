using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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

    public ScanResult RunAll(ScanContext context)
    {
        foreach (ICheck check in _checks)
        {
            var sw = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource();
            
#if NET8_0
            cts.Token.Register(() => 
            {
                Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | Orchestrator | thread abandoned for {check.Name} after {sw.ElapsedMilliseconds}ms.");
            });
#endif

            Thread workerThread = new Thread(() =>
            {
                try
                {
                    CheckResult result = check.Run(context, cts.Token);
                    if (!result.Success)
                    {
                        context.Result.CheckErrors[check.Name] = result.Error ?? "Unknown error";
                    }
                }
#if NET35
                catch (ThreadAbortException)
                {
                    Thread.ResetAbort();
                }
#endif
                catch (Exception ex)
                {
                    context.Result.CheckErrors[check.Name] = $"Unhandled exception: {ex.Message}";
                }
            });
            
            workerThread.IsBackground = true;
            workerThread.Start();
            
            bool completed = false;
            while (sw.Elapsed < _budget)
            {
                if (workerThread.Join(1000))
                {
                    completed = true;
                    break;
                }
            }
            
            if (!completed)
            {
                cts.Cancel();
                
#if NET35
                try { workerThread.Abort(); } catch { }
#endif
                Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | Orchestrator | Module {check.Name} timed out after {sw.ElapsedMilliseconds}ms.");
                context.Result.CheckErrors[check.Name] = "Scan aborted: exceeded 90 second budget.";
            }
        }

        int totalChecks = 0;
        foreach (var _ in _checks) totalChecks++;
        int failedChecks = context.Result.CheckErrors.Count;
        int successfulChecks = totalChecks - failedChecks;
        if (totalChecks > 0)
        {
            context.Result.ScanCompleteness = $"{Math.Round((double)successfulChecks / totalChecks * 100)}%";
        }

        return context.Result;
    }
}

