using System;
using Xunit;
using VaxDrive.VaxAgent.Checks;

namespace VaxDrive.VaxAgent.Tests.Checks;

public class CveMatchCheckTests
{
    [Theory]
    [InlineData("1.2.3", "1.0.0", "2.0.0", true)]
    [InlineData("0.9.0", "1.0.0", "2.0.0", false)]
    [InlineData("2.1.0", "1.0.0", "2.0.0", false)]
    [InlineData("1.2.3", null, "2.0.0", true)]
    [InlineData("bad", "1.0.0", "2.0.0", false)]
    [InlineData("1.5.0-beta", "1.0.0", "2.0.0", true)] // Tests pre-release suffix ignore
    public void TryParseSemVer_BoundsCheck_WorksCorrectly(string version, string minBound, string maxBound, bool expectedMatch)
    {
        if (!CveMatchCheck.TryParseSemVer(version, out Version parsedVersion))
        {
            // If it can't parse, it doesn't match and shouldn't throw
            Assert.False(expectedMatch);
            return;
        }

        bool match = true;

        if (!string.IsNullOrEmpty(minBound))
        {
            if (CveMatchCheck.TryParseSemVer(minBound, out Version minVersion))
            {
                if (parsedVersion < minVersion) match = false;
            }
        }

        if (!string.IsNullOrEmpty(maxBound))
        {
            if (CveMatchCheck.TryParseSemVer(maxBound, out Version maxVersion))
            {
                if (parsedVersion > maxVersion) match = false;
            }
        }

        Assert.Equal(expectedMatch, match);
    }
}
