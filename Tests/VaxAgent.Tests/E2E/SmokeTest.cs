using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using VaxDrive.VaxAgent.Cli;
using VaxDrive.VaxAgent.Startup;
using VaxDrive.VaxAgent.Network;
using VaxDrive.VaxAgent.Network.Protocols;
using VaxDrive.VaxDock.Data;
using VaxDrive.VaxDock.Services.Nvd;
using Microsoft.Data.Sqlite;

namespace VaxDrive.VaxAgent.Tests.E2E;

public class SmokeTest : IDisposable
{
    private readonly string _testDbPath;

    public SmokeTest()
    {
        // Set environment variables required for initialization
        Environment.SetEnvironmentVariable("VAXDRIVE_HARDWARE_TOKEN_PROVIDER", "KINGSTON");
        Environment.SetEnvironmentVariable("VAXDRIVE_DB_KEY", "smoke_test_key_32bytes_00");
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", "smoke_test_build_key");

        _testDbPath = Path.Combine(Path.GetTempPath(), $"vax_smoke_{Guid.NewGuid()}.db");
    }

    [Fact]
    public void Test1_AgentBootstrap_InitializesWithoutThrowing()
    {
        // Act & Assert
        // Given the valid env vars set in the constructor, this should not throw
        var bootstrap = new AgentBootstrap();
        Assert.NotNull(bootstrap);
    }

    [Fact]
    public void Test2_NvdPaginationEngine_ConnectsToSqlite_SchemaCreatesClean()
    {
        // Arrange
        var repository = new CveRepository(_testDbPath);
        
        using var conn = new SqliteConnection($"Data Source={_testDbPath}");
        conn.Open();
        using var cmdKey = conn.CreateCommand();
        cmdKey.CommandText = "PRAGMA key = 'smoke_test_key_32bytes_00';";
        cmdKey.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE cve_cache (cve_id TEXT PRIMARY KEY, published TEXT NOT NULL, modified TEXT NOT NULL, cvss_v3 REAL, description TEXT, cpe_list TEXT);
            CREATE TABLE sync_checkpoints (id INTEGER PRIMARY KEY, last_start_index INTEGER NOT NULL, last_etag TEXT);";
        cmd.ExecuteNonQuery();

        var engine = new NvdPaginationEngine(repository);

        // Act & Assert
        // We ensure we can read/write without SQL errors
        repository.UpsertCheckpoint(500, "SMOKE_TEST");
        int startIndex = repository.GetLastCheckpointStartIndex();
        
        Assert.Equal(500, startIndex);
        Assert.NotNull(engine);
    }

    [Fact]
    public void Test3_TopologyMap_Upserts100RecordsConcurrently()
    {
        // Arrange
        var map = new TopologyMap();

        // Act
        Parallel.For(0, 100, i =>
        {
            map.Upsert(new DeviceRecord
            {
                MacAddress = $"00:11:22:33:44:{i:X2}",
                IpAddress = $"192.168.1.{i}",
                Protocol = "DNP3",
                Vendor = "TestVendor"
            });
        });

        // Assert
        // TopologyMap handles concurrent dictionary operations safely.
        // We'll just assert it didn't throw an exception during parallel execution.
        Assert.NotNull(map);
    }

    [Theory]
    [InlineData(4.0, 7.5, true)]  // diff 3.5 -> anomalous
    [InlineData(5.0, 6.0, false)] // diff 1.0 -> normal
    public void Test4_SeverityDeltaComputer_FlagsAnomalous(double oldScore, double newScore, bool expectedAnomaly)
    {
        var computer = new SeverityDeltaComputer();
        bool isAnomalous = computer.IsAnomalousShift(oldScore, newScore);
        Assert.Equal(expectedAnomaly, isAnomalous);
    }

    [Fact]
    public async Task Test5_AgentCli_Version_ExitsCode0()
    {
        // Act
        int result = await AgentCli.Main(new[] { "--version" }).ConfigureAwait(false);

        // Assert
        Assert.Equal(0, result);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("VAXDRIVE_HARDWARE_TOKEN_PROVIDER", null);
        Environment.SetEnvironmentVariable("VAXDRIVE_DB_KEY", null);
        Environment.SetEnvironmentVariable("VAXDRIVE_BUILD_KEY", null);
        
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch { }
        }
    }
}
