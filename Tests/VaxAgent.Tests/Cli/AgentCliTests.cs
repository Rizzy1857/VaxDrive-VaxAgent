using System;
using System.Threading.Tasks;
using Xunit;
using VaxDrive.VaxAgent.Cli;

namespace VaxDrive.VaxAgent.Tests.Cli;

public class AgentCliTests
{
    [Theory]
    [InlineData(new string[] { }, 2)]
    [InlineData(new string[] { "--unknown" }, 2)]
    public async Task Main_InvalidArgs_ReturnsExitCode2(string[] args, int expectedExitCode)
    {
        // Act
        int result = await AgentCli.Main(args).ConfigureAwait(false);

        // Assert
        Assert.Equal(expectedExitCode, result);
    }

    [Fact]
    public async Task Main_VersionArg_ReturnsExitCode0()
    {
        // Arrange
        // Environment variables needed to pass AgentBootstrap initialization
        Environment.SetEnvironmentVariable("VAXDRIVE_HARDWARE_TOKEN_PROVIDER", "KINGSTON");
        
        try
        {
            // Act
            int result = await AgentCli.Main(new string[] { "--version" }).ConfigureAwait(false);

            // Assert
            Assert.Equal(0, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VAXDRIVE_HARDWARE_TOKEN_PROVIDER", null);
        }
    }
}
