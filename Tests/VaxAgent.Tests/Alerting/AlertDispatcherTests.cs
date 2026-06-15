using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using VaxDrive.VaxAgent.Alerting;

namespace VaxDrive.VaxAgent.Tests.Alerting;

public class AlertDispatcherTests : IDisposable
{
    private readonly string _testDir;

    public AlertDispatcherTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"vax_alert_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        Environment.SetEnvironmentVariable("VAXDRIVE_ALERT_LOG_PATH", _testDir);
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", "test_hmac_key_123");
        Environment.SetEnvironmentVariable("VAXDRIVE_SYSLOG_HOST", "DISABLED");
    }

    [Fact]
    public void FormatCef_ReturnsCorrectlyFormattedString()
    {
        // Arrange
        var dispatcher = new AlertDispatcher();

        // Act
        string cef = dispatcher.FormatCef(1001, "YaraMatch_Industroyer", 8, "msg=Found anomalous block");

        // Assert
        Assert.StartsWith("CEF:0|VaxDrive|VaxAgent|", cef);
        Assert.EndsWith("|1001|YaraMatch_Industroyer|8|msg=Found anomalous block", cef);
    }

    [Fact]
    public async Task DispatchAlertAsync_CreatesLogFileWithRolloverFormat()
    {
        // Arrange
        var dispatcher = new AlertDispatcher();

        // Act
        await dispatcher.DispatchAlertAsync(1001, "TestAlert", 5, "msg=test").ConfigureAwait(false);

        // Assert
        string expectedFilename = $"alerts_{DateTime.UtcNow.ToString("yyyyMMdd")}.log";
        string expectedPath = Path.Combine(_testDir, expectedFilename);

        Assert.True(File.Exists(expectedPath));

        string content = await File.ReadAllTextAsync(expectedPath).ConfigureAwait(false);
        Assert.Contains("CEF:0|VaxDrive|VaxAgent", content);
        Assert.Contains("[HMAC:", content);
    }

    [Fact]
    public async Task DispatchAlertAsync_MissingEnvVars_SkipsSilentlyWithoutThrowing()
    {
        // Arrange
        Environment.SetEnvironmentVariable("VAXDRIVE_ALERT_LOG_PATH", null);
        Environment.SetEnvironmentVariable("VAXDRIVE_SYSLOG_HOST", null);

        var dispatcher = new AlertDispatcher();

        // Act & Assert
        // This should complete successfully without any exceptions or file creation
        var exception = await Record.ExceptionAsync(() => dispatcher.DispatchAlertAsync(1001, "Test", 5, ""));
        Assert.Null(exception);
    }

    [Fact]
    public async Task SendToSyslogAsync_Failure_QueuesMessage()
    {
        // Arrange
        Environment.SetEnvironmentVariable("VAXDRIVE_SYSLOG_HOST", "invalid.local.domain.internal.fail");
        using var dispatcher = new AlertDispatcher();

        // Act
        await dispatcher.SendToSyslogAsync("test payload", false);

        // Assert
        var queueField = typeof(AlertDispatcher).GetField("_retryQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var queue = queueField!.GetValue(dispatcher);
        
        int count = (int)queue!.GetType().GetProperty("Count")!.GetValue(queue)!;
        Assert.Equal(1, count);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("VAXDRIVE_ALERT_LOG_PATH", null);
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", null);
        Environment.SetEnvironmentVariable("VAXDRIVE_SYSLOG_HOST", null);

        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }
}
