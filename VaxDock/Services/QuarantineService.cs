using System;
using Microsoft.Data.Sqlite;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Services;

public sealed class QuarantineService
{
    public void Quarantine(string filename, string reason)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO QuarantinedFiles (Filename, FailureReason, DetectedAt)
            VALUES (@filename, @reason, @now)";
        
        cmd.Parameters.AddWithValue("@filename", filename);
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        
        cmd.ExecuteNonQuery();
    }
}
