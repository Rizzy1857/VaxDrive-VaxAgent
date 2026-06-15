using System;
using System.Threading.Tasks;
using Xunit;
using VaxDrive.VaxAgent.Cli;

namespace VaxDrive.VaxAgent.Tests.Cli;

[Collection("AgentEnv")]
public class AgentCliTests : IDisposable
{
    private readonly string? _oldProvider;

    public AgentCliTests()
    {
        _oldProvider = Environment.GetEnvironmentVariable("VAXDRIVE_HARDWARE_TOKEN_PROVIDER");
        Environment.SetEnvironmentVariable("VAXDRIVE_HARDWARE_TOKEN_PROVIDER", "MOCK");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("VAXDRIVE_HARDWARE_TOKEN_PROVIDER", _oldProvider);
    }

    [Theory]
    [InlineData(new string[] { }, 2)]
    [InlineData(new string[] { "--unknown" }, 2)]
    public async Task Main_InvalidArgs_ReturnsExitCode2(string[] args, int expectedExitCode)
    {
        // Act
        int result = await AgentCli.Main(args);

        // Assert
        Assert.Equal(expectedExitCode, result);
    }

    [Fact]
    public async Task Main_VersionArg_ReturnsExitCode0()
    {
        // Act
        int result = await AgentCli.Main(new string[] { "--version" });

        // Assert
        Assert.Equal(0, result);
    }
}
