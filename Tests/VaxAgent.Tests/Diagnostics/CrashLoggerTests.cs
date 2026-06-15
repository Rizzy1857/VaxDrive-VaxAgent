using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using VaxDrive.VaxAgent.Diagnostics;

namespace VaxDrive.VaxAgent.Tests.Diagnostics;

[Collection("AgentEnv")]
public class CrashLoggerTests : IDisposable
{
    private string _tempKey;

    public CrashLoggerTests()
    {
        _tempKey = Environment.GetEnvironmentVariable("VAXDRIVE_BUILD_KEY") ?? "";
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", "TestBuildKey123");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", _tempKey);
        
        // Cleanup log files
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        foreach (var file in Directory.GetFiles(dir, "crash_*.log"))
        {
            try { File.Delete(file); }
            catch (System.IO.IOException) { }
            catch (System.ObjectDisposedException) { }
        }
        foreach (var file in Directory.GetFiles(dir, "UNSIGNED_crash_*.log"))
        {
            try { File.Delete(file); }
            catch (System.IO.IOException) { }
            catch (System.ObjectDisposedException) { }
        }
    }

    [Fact]
    public void WriteCrashLog_ValidException_CreatesSignedFileWithStackTrace()
    {
        Exception ex;
        try
        {
            throw new InvalidOperationException("Test crash from unit test");
        }
        catch (InvalidOperationException e)
        {
            ex = e;
        }

        CrashLogger.WriteCrashLog(ex);

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        var files = Directory.GetFiles(dir, "crash_*.log");
        Assert.Single(files);

        string content = File.ReadAllText(files[0]);
        Assert.Contains("[HMAC:", content);
        Assert.Contains("Test crash from unit test", content);
        Assert.Contains(nameof(InvalidOperationException), content);
        Assert.Contains("WriteCrashLog_ValidException_CreatesSignedFileWithStackTrace", content); // part of stack trace
    }

    [Fact]
    public void WriteCrashLog_NoKey_CreatesUnsignedFile()
    {
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", "");

        Exception ex = new Exception("No key test");
        CrashLogger.WriteCrashLog(ex);

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        var files = Directory.GetFiles(dir, "UNSIGNED_crash_*.log");
        Assert.Single(files);

        string content = File.ReadAllText(files[0]);
        Assert.DoesNotContain("[HMAC:", content);
        Assert.Contains("No key test", content);
    }
}
