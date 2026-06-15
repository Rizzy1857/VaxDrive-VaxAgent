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
        await task;

        // Assert
        // The staging directory should NOT be created because it aborted early
        string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        string stagingDir = Path.Combine(Directory.GetParent(currentDir)?.FullName ?? currentDir, "updates_staging");
        Assert.False(Directory.Exists(stagingDir));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ValidHashes_CreatesStagingAndFlag()
    {
        // Arrange
        var service = new SelfUpdateService();
        string manifestPath = Path.Combine(_testDir, "manifest.json");
        string filePath = Path.Combine(_testDir, "test.dll");
        
        File.WriteAllText(filePath, "Mock DLL Content");
        
        string actualHash;
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            actualHash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        string filesJson = $@"[
            {{
                ""filename"": ""test.dll"",
                ""sha256"": ""{actualHash}"",
                ""size_bytes"": 16
            }}
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
        var method = typeof(SelfUpdateService).GetMethod("CheckForUpdatesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        using var cts = new CancellationTokenSource();
        var task = (Task)method!.Invoke(service, new object[] { cts.Token })!;
        await task;

        // Assert
        string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        string rootDir = Directory.GetParent(currentDir)?.FullName ?? currentDir;
        string stagingDir = Path.Combine(rootDir, "updates_staging");
        string flagFile = Path.Combine(rootDir, "staged_ready.flag");

        Assert.True(Directory.Exists(stagingDir));
        Assert.True(File.Exists(Path.Combine(stagingDir, "test.dll")));
        Assert.True(File.Exists(flagFile));

        // Cleanup
        if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
        if (File.Exists(flagFile)) File.Delete(flagFile);
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
