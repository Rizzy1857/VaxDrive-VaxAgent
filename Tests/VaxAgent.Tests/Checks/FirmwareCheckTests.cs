using System;
using Xunit;
using VaxDrive.VaxAgent.Checks;

namespace VaxDrive.VaxAgent.Tests.Checks;

public class FirmwareCheckTests
{
    [Theory]
    [InlineData(0, 1.0)] // all 4 present -> 1.0
    [InlineData(2, 0.5)] // 2 missing -> 0.5
    [InlineData(4, 0.0)] // all missing -> 0.0
    public void ConfidenceScore_CalculatesCorrectly(int missingCount, double expectedScore)
    {
        double actualScore = FirmwareCheck.CalculateConfidence(missingCount);
        Assert.Equal(expectedScore, actualScore);
    }
}
