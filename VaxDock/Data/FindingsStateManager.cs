using System;
using Microsoft.Data.Sqlite;

namespace VaxDrive.VaxDock.Data;

public sealed class FindingsStateManager
{
    public void MarkResolved(int findingId)
    {
        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Findings 
            SET Status = 'Resolved', ResolvedAt = @now 
            WHERE Id = @id AND Status != 'Resolved'";
        cmd.Parameters.AddWithValue("@id", findingId);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void Escalate(int findingId)
    {
        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Findings 
            SET Status = 'Escalated', EscalatedAt = @now 
            WHERE Id = @id AND Status != 'Escalated'";
        cmd.Parameters.AddWithValue("@id", findingId);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }
    
    public void Verify(int findingId)
    {
        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Findings 
            SET Status = 'Verified' 
            WHERE Id = @id AND Status = 'Resolved'"; // Only resolved findings can be formally verified
        cmd.Parameters.AddWithValue("@id", findingId);
        cmd.ExecuteNonQuery();
    }
}
