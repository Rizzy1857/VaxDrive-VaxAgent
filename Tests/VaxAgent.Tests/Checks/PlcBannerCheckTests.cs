using System;
using System.Threading;
using Xunit;
using VaxDrive.Models;
using VaxDrive.VaxAgent.Checks;

namespace VaxDrive.VaxAgent.Tests.Checks;

public class PlcBannerCheckTests
{
    [Fact]
    public void Run_NoPcapNoAdmin_ReturnsEmptyListWithoutThrowing()
    {
        var check = new PlcBannerCheck();
        var context = new ScanContext();
        var ct = new CancellationToken();

        // This will likely fail PassiveArpListener due to missing SharpPcap or active network,
        // then fail the raw socket due to lack of admin/root privileges in the test runner.
        // The check should swallow the exceptions, log them, and return Ok with an empty list.
        var result = check.Run(context, ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.Result.PlcNeighbors);
    }
}
