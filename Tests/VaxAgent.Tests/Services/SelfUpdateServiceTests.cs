using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using VaxDrive.VaxAgent.Services;

namespace VaxDrive.VaxAgent.Tests.Services;

public class SelfUpdateServiceTests : IDisposable
{
    private readonly string _testDir;
    
    public SelfUpdateServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"vax_update_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", "test_build_key");
        Environment.SetEnvironmentVariable("VAXDRIVE_UPDATE_PATH", _testDir);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_HashMismatch_AbortsUpdate()
    {
        // Arrange
        var service = new SelfUpdateService();
        string manifestPath = Path.Combine(_testDir, "manifest.json");
        string filePath = Path.Combine(_testDir, "test.dll");
        
        File.WriteAllText(filePath, "Mock DLL Content");
        
        string filesJson = @"[
            {
                ""filename"": ""test.dll"",
                ""sha256"": ""fake_invalid_hash"",
                ""size_bytes"": 16
            }
        ]";

        byte[] keyBytes = Encoding.UTF8.GetBytes("test_build_key");
        byte[] dataBytes = Encoding.UTF8.GetBytes(filesJson);
        using var hmac = new HMACSHA256(keyBytes);
        string sig = Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLowerInvariant();

        string manifestJson = $@"{{
            ""signature"": ""{sig}"",
            ""files"": {filesJson}
        }}";
        
        File.WriteAllText(manifestPath, manifestJson);

        // Act
        // Use reflection to invoke the private async method for testing
        var method = typeof(SelfUpdateService).GetMethod("CheckForUpdatesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)method!.Invoke(service, new object[] { cts.Token })!;
        await task.ConfigureAwait(false);

        // Assert
        // The update temp directory should NOT be created because it aborted early
        string tempUpdateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".update_temp");
        Assert.False(Directory.Exists(tempUpdateDir));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", null);
        Environment.SetEnvironmentVariable("VAXDRIVE_UPDATE_PATH", null);
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }
}
