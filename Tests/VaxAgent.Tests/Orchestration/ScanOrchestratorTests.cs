using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using VaxDrive.Models;
using VaxDrive.VaxAgent.Checks;

namespace VaxDrive.VaxAgent.Tests.Orchestration;

public class ScanOrchestratorTests
{
    private class BlockingCheck : ICheck
    {
        public string Name => "BlockingCheck";

        public CheckResult Run(ScanContext context, CancellationToken ct)
        {
            // Simulate a hang that ignores cancellation
            Thread.Sleep(95000); // Exceeds 90s budget
            return CheckResult.Ok();
        }
    }

    private class FastCheck : ICheck
    {
        public string Name => "FastCheck";

        public CheckResult Run(ScanContext context, CancellationToken ct)
        {
            context.Result.Findings.Add(new Finding { Component = "FastModuleExecuted" });
            return CheckResult.Ok();
        }
    }

    [Fact]
    public void RunAll_ModuleExceedsBudget_KillsModuleAndContinues()
    {
        // To avoid waiting 90s in a unit test, we'll use reflection to inject a smaller budget for the test.
        // Wait, the budget is hardcoded to TimeSpan.FromSeconds(90).
        // Let's use reflection to set _budget to 2 seconds.
        var orchestrator = new ScanOrchestrator(new ICheck[] { new BlockingCheck(), new FastCheck() });
        var budgetField = typeof(ScanOrchestrator).GetField("_budget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        budgetField.SetValue(orchestrator, TimeSpan.FromSeconds(2));

        var context = new ScanContext();
        
        var result = orchestrator.RunAll(context);

        Assert.True(result.CheckErrors.ContainsKey("BlockingCheck"));
        Assert.Equal("Scan aborted: exceeded 90 second budget.", result.CheckErrors["BlockingCheck"]);
        
        // Ensure the next module ran
        Assert.Contains(result.Findings, f => f.Component == "FastModuleExecuted");
    }
}
