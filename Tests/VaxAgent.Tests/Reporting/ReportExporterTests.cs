using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using VaxDrive.VaxAgent.Checks.Yara;
using VaxDrive.VaxAgent.Network;
using VaxDrive.VaxDock.Data;
using VaxDrive.VaxAgent.Reporting;

namespace VaxDrive.VaxAgent.Tests.Reporting;

public class ReportExporterTests : IDisposable
{
    private readonly string _testDir;

    public ReportExporterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"vax_report_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        Environment.SetEnvironmentVariable("VAXDRIVE_ROOT", _testDir);
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", "test_hmac_key_123");
    }

    [Fact]
    public async Task ExportReportAsync_GeneratesHtmlAndJsonWithSignatures()
    {
        // Arrange
        var exporter = new ReportExporter();
        
        var topology = new TopologyMap(); // Assuming GetAssets returns empty list
        var yaraHits = new List<YaraMatch>();
        var deltas = new List<DeltaRecord>();

        // Act
        await exporter.ExportReportAsync(topology, yaraHits, deltas);

        // Assert
        var files = Directory.GetFiles(_testDir);
        
        // Test 1: HTML file exists
        Assert.Contains(files, f => f.EndsWith(".html"));
        Assert.Contains(files, f => f.EndsWith(".json"));

        // Test 2: .sig file exists and HMAC validates
        string htmlPath = Array.Find(files, f => f.EndsWith(".html"))!;
        string sigPath = htmlPath + ".sig";
        Assert.True(File.Exists(sigPath));

        string htmlContent = await File.ReadAllTextAsync(htmlPath);
        string expectedSig = await File.ReadAllTextAsync(sigPath);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("test_hmac_key_123"));
        string computedSig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(htmlContent))).ToLowerInvariant();
        
        Assert.Equal(expectedSig, computedSig);

        // Test 3: No env vars or key material in HTML output
        Assert.DoesNotContain("VAXDRIVE_BUILD_KEY", htmlContent);
        Assert.DoesNotContain("test_hmac_key_123", htmlContent);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("VAXDRIVE_ROOT", null);
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", null);

        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }
}
