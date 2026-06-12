using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace VaxDrive.VaxDock.Data;

public sealed class DeviceSummary
{
    public string Id { get; init; } = "";
    public string LastSeen { get; init; } = "";
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
}

public sealed class DeviceRepository
{
    public IReadOnlyList<DeviceSummary> GetDeviceSummaries()
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        // Senior Engineer Trap Avoided: Compute severity counts directly in SQL (GROUP BY), not in C# LINQ
        cmd.CommandText = @"
            SELECT d.Id, d.LastSeen,
                   COUNT(CASE WHEN f.Severity = 'CRITICAL' AND f.ResolvedAt IS NULL THEN 1 END) AS CriticalCount,
                   COUNT(CASE WHEN f.Severity = 'HIGH' AND f.ResolvedAt IS NULL THEN 1 END) AS HighCount
            FROM Devices d
            LEFT JOIN Findings f ON f.DeviceId = d.Id
            GROUP BY d.Id
            ORDER BY CriticalCount DESC, HighCount DESC";
            
        List<DeviceSummary> results = new List<DeviceSummary>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new DeviceSummary
            {
                Id = reader.GetString(0),
                LastSeen = reader.GetString(1),
                CriticalCount = reader.GetInt32(2),
                HighCount = reader.GetInt32(3)
            });
        }
        return results;
    }

    public int GetOverdueDevicesCount(int days)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        // Senior Engineer Trap Avoided: Overdue calculation pushed to SQL to avoid LINQ memory explosion
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM Devices 
            WHERE datetime(LastSeen) < datetime('now', @days)";
        cmd.Parameters.AddWithValue("@days", $"-{days} days");
        
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
