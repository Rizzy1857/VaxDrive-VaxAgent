using System;
using System.Linq;
using Xunit;
using VaxDrive.VaxDock.Data;
using VaxDrive.VaxDock.Services.Nvd;
using Microsoft.Data.Sqlite;

namespace VaxDrive.VaxAgent.Tests.Integration;

public class NvdRepositoryIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CveRepository _repository;

    public NvdRepositoryIntegrationTests()
    {
        Environment.SetEnvironmentVariable("VAXDRIVE_DB_KEY", "test_key_32bytes_padding_here00");
        
        // Use an actual file for tests instead of pure in-memory, since SQLite PRAGMA KEY 
        // with in-memory can sometimes be tricky across multiple connections if not shared cache.
        _dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vax_test_{Guid.NewGuid()}.db");
        
        _repository = new CveRepository(_dbPath);

        // Run Schema setup
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmdKey = conn.CreateCommand();
        cmdKey.CommandText = "PRAGMA key = 'test_key_32bytes_padding_here00';";
        cmdKey.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS cve_cache (
                cve_id TEXT PRIMARY KEY,
                published TEXT NOT NULL,
                modified TEXT NOT NULL,
                cvss_v3 REAL,
                description TEXT,
                cpe_list TEXT
            );
            CREATE TABLE IF NOT EXISTS severity_deltas (
                cve_id TEXT,
                old_score REAL,
                new_score REAL,
                recorded_at TEXT
            );
            CREATE TABLE IF NOT EXISTS sync_checkpoints (
                id INTEGER PRIMARY KEY,
                last_start_index INTEGER NOT NULL,
                last_etag TEXT
            );";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Test1_UpsertCve_SearchByCpe_ReturnsCorrectRecord()
    {
        var record = new CveRecord
        {
            CveId = "CVE-2023-1234",
            Published = "2023-01-01T00:00:00Z",
            Modified = "2023-01-02T00:00:00Z",
            CvssV3 = 9.8,
            Description = "Test Vuln",
            CpeList = "cpe:2.3:o:microsoft:windows_10:*:*:*:*:*:*:*:*"
        };

        _repository.UpsertCve(record);

        var results = _repository.SearchByCpe("windows_10");

        Assert.Single(results);
        Assert.Equal("CVE-2023-1234", results[0].CveId);
        Assert.Equal(9.8, results[0].CvssV3);
    }

    [Fact]
    public void Test2_CpeWithWildcards_DoesNotBreakQuery()
    {
        var record = new CveRecord
        {
            CveId = "CVE-2023-9999",
            Published = "2023-01-01T00:00:00Z",
            Modified = "2023-01-01T00:00:00Z",
            CvssV3 = 5.0,
            Description = "Test",
            CpeList = "cpe:2.3:a:vendor:product_name%with_wildcards:*:*:*:*:*:*:*:*"
        };

        _repository.UpsertCve(record);

        // Search for the literal string containing % and _
        var results = _repository.SearchByCpe("product_name%with_wildcards");

        Assert.Single(results);
        Assert.Equal("CVE-2023-9999", results[0].CveId);
    }

    [Fact]
    public void Test3_StoreSeverityDelta_GetDelta_RespectsCutoff()
    {
        _repository.StoreSeverityDelta("CVE-OLD", 5.0, 8.0); // Will be recorded 'now'

        DateTime cutoff = DateTime.UtcNow.AddMinutes(-5);
        DateTime futureCutoff = DateTime.UtcNow.AddMinutes(5);

        var allSincePast = _repository.GetDelta(cutoff);
        var allSinceFuture = _repository.GetDelta(futureCutoff);

        Assert.Contains(allSincePast, d => d.CveId == "CVE-OLD");
        Assert.Empty(allSinceFuture);
    }

    [Theory]
    [InlineData(4.0, 7.5, true)]
    [InlineData(5.0, 7.9, false)]
    [InlineData(9.8, 9.8, false)]
    [InlineData(2.0, 6.0, true)]
    public void Test4_IsAnomalousShift_EvaluatesCorrectly(double oldScore, double newScore, bool expectedAnomaly)
    {
        var computer = new SeverityDeltaComputer();
        bool isAnomalous = computer.IsAnomalousShift(oldScore, newScore);
        Assert.Equal(expectedAnomaly, isAnomalous);
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(_dbPath))
        {
            try
            {
                System.IO.File.Delete(_dbPath);
            }
            catch { }
        }
    }
}
