using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Tests.Data;

[Collection("Database")]
public sealed class FindingsStateManagerTests : IDisposable
{
    private readonly string _dbPath;

    public FindingsStateManagerTests()
    {
        _dbPath = Path.GetTempFileName() + ".db";
        DatabaseBootstrap.Initialize(_dbPath);

        // Seed necessary tables and a finding
        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Devices (Id, LastSeen) VALUES ('DEV1', '2026-06-12T00:00:00Z');
            INSERT INTO Scans (ScanId, DeviceId, Timestamp, RawJson, IngestedAt) 
                VALUES ('SCAN1', 'DEV1', '2026-06-12T00:00:00Z', '{}', '2026-06-12T00:00:00Z');
            INSERT INTO Findings (Id, ScanId, DeviceId, CveId, Severity, Component, Status, DefinitionsPackVersion)
                VALUES (1, 'SCAN1', 'DEV1', 'CVE-1', 'HIGH', 'OS', 'Open', '1.0.0');
        ";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void MarkResolved_UpdatesStatusAndTimestamp()
    {
        var manager = new FindingsStateManager();
        manager.MarkResolved(1);

        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Status, ResolvedAt FROM Findings WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Resolved", reader.GetString(0));
        Assert.False(reader.IsDBNull(1));
    }

    [Fact]
    public void Verify_OnlyUpdatesResolvedFindings()
    {
        var manager = new FindingsStateManager();
        
        // Finding is currently 'Open'
        manager.Verify(1);
        
        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Status FROM Findings WHERE Id = 1";
        Assert.Equal("Open", cmd.ExecuteScalar()); // Should remain open

        // Now resolve it
        manager.MarkResolved(1);
        manager.Verify(1);
        Assert.Equal("Verified", cmd.ExecuteScalar()); // Should update to verified
    }

    public void Dispose()
    {
        var conn = DatabaseBootstrap.GetConnection();
        conn.Close();
        conn.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
