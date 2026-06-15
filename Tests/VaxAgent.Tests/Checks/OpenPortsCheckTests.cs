using System;
using Xunit;
using VaxDrive.VaxAgent.Checks;
using VaxDrive.Models;

namespace VaxDrive.VaxAgent.Tests.Checks;

public class OpenPortsCheckTests
{
    [Theory]
    [InlineData("  TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       1124", 135, true)] // English LISTENING
    [InlineData("  TCP    0.0.0.0:445            0.0.0.0:0              WARTEND         4", 445, true)] // German WARTEND
    [InlineData("  TCP    0.0.0.0:3389           0.0.0.0:0              EN ECOUTE       1234", 3389, true)] // French EN ECOUTE
    [InlineData("  TCP    192.168.1.10:443       10.0.0.5:12345         ESTABLISHED     1124", 443, false)] // ESTABLISHED (not 0.0.0.0:0)
    [InlineData("  TCP    [::]:80                [::]:0                 LISTENING       444", 80, true)] // IPv6
    [InlineData("Malformed non-TCP row data that is totally invalid", 0, false)] // Malformed
    public void ParseNetstatOutput_HandlesLocalizedStateViaRegex(string outputLine, int expectedPort, bool expectFound)
    {
        var context = new ScanContext();
        
        OpenPortsCheck.ParseNetstatOutput(outputLine, context);
        
        if (expectFound)
        {
            Assert.Contains(expectedPort, context.Result.OpenPorts);
        }
        else
        {
            Assert.DoesNotContain(expectedPort, context.Result.OpenPorts);
        }
    }
}
