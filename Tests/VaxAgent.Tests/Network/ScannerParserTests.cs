using System;
using Xunit;
using VaxDrive.VaxAgent.Network;

namespace VaxDrive.VaxAgent.Tests.Network;

public sealed class ScannerParserTests
{
    [Fact]
    public void S7Scanner_ParseBanner_MalformedShortResponse_ReturnsNull()
    {
        byte[] buffer = new byte[10];
        string? result = S7Scanner.ParseBanner(buffer, 10);
        Assert.Null(result);
    }

    [Fact]
    public void S7Scanner_ParseBanner_ValidResponse_ExtractsCleanAscii()
    {
        byte[] buffer = new byte[60];
        // Populate with non-printable junk
        for (int i = 0; i < 60; i++) buffer[i] = 0x01;
        
        // Inject "Siemens S7-1500" into the middle
        byte[] banner = System.Text.Encoding.ASCII.GetBytes("Siemens S7-1500");
        Array.Copy(banner, 0, buffer, 42, banner.Length);

        string? result = S7Scanner.ParseBanner(buffer, 60);
        
        Assert.NotNull(result);
        Assert.Contains("Siemens S7-1500", result);
        Assert.DoesNotContain("\x01", result); // Non-printable should be sanitized
    }

    [Fact]
    public void ModbusScanner_ParseBanner_MalformedShortResponse_ReturnsNull()
    {
        byte[] buffer = new byte[5];
        string? result = ModbusScanner.ParseBanner(buffer, 5);
        Assert.Null(result);
    }

    [Fact]
    public void ModbusScanner_ParseBanner_ValidResponse_ExtractsCleanAscii()
    {
        byte[] buffer = new byte[30];
        // Populate with non-printable junk
        for (int i = 0; i < 30; i++) buffer[i] = 0x02;
        
        // Inject "Schneider M241"
        byte[] banner = System.Text.Encoding.ASCII.GetBytes("Schneider M241");
        Array.Copy(banner, 0, buffer, 11, banner.Length);

        string? result = ModbusScanner.ParseBanner(buffer, 30);
        
        Assert.NotNull(result);
        Assert.Contains("Schneider M241", result);
        Assert.DoesNotContain("\x02", result); // Non-printable should be sanitized
    }
}
