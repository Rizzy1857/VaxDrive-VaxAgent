using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;
using VaxDrive.VaxDock.Data;
using VaxDrive.VaxDock.Services;

namespace VaxDrive.VaxDock.Tests.Services;

public sealed class QuarantineServiceTests : IDisposable
{
    private readonly string _dbPath;

    public QuarantineServiceTests()
    {
        _dbPath = Path.GetTempFileName() + ".db";
        DatabaseBootstrap.Initialize(_dbPath);
    }

    [Fact]
    public void Quarantine_ValidEntry_InsertsIntoDatabase()
    {
        var service = new QuarantineService();
        service.Quarantine("SCAN_123.vax", "HMAC Mismatch");

        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM QuarantinedFiles WHERE Filename = 'SCAN_123.vax'";
        long count = (long)cmd.ExecuteScalar()!;
        
        Assert.Equal(1, count);
    }

    public void Dispose()
    {
        DatabaseBootstrap.GetConnection().Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
