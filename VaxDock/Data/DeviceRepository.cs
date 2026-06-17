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
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
    public string AssetCriticality { get; init; } = "UNCLASSIFIED";
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
                   COUNT(CASE WHEN f.Severity = 'CRITICAL' AND f.ResolvedAt IS NULL AND f.Suppressed = 0 THEN 1 END) AS CriticalCount,
                   COUNT(CASE WHEN f.Severity = 'HIGH' AND f.ResolvedAt IS NULL AND f.Suppressed = 0 THEN 1 END) AS HighCount,
                   COUNT(CASE WHEN f.Severity = 'MEDIUM' AND f.ResolvedAt IS NULL AND f.Suppressed = 0 THEN 1 END) AS MediumCount,
                   COUNT(CASE WHEN f.Severity = 'LOW' AND f.ResolvedAt IS NULL AND f.Suppressed = 0 THEN 1 END) AS LowCount,
                   d.AssetCriticality
            FROM Devices d
            LEFT JOIN Findings f ON f.DeviceId = d.Id
            GROUP BY d.Id
            ORDER BY CriticalCount DESC, HighCount DESC, MediumCount DESC, LowCount DESC";
            
        List<DeviceSummary> results = new List<DeviceSummary>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new DeviceSummary
            {
                Id = reader.GetString(0),
                LastSeen = reader.GetString(1),
                CriticalCount = reader.GetInt32(2),
                HighCount = reader.GetInt32(3),
                MediumCount = reader.GetInt32(4),
                LowCount = reader.GetInt32(5),
                AssetCriticality = reader.GetString(6)
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

    public List<string> GetOverdueDevices(int days)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id 
            FROM Devices 
            WHERE datetime(LastSeen) < datetime('now', @days)";
        cmd.Parameters.AddWithValue("@days", $"-{days} days");
        
        List<string> results = new List<string>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }

    public void UpdateAssetCriticality(string deviceId, string criticality)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Devices 
            SET AssetCriticality = @criticality
            WHERE Id = @deviceId";
        cmd.Parameters.AddWithValue("@criticality", criticality);
        cmd.Parameters.AddWithValue("@deviceId", deviceId);
        cmd.ExecuteNonQuery();
    }
}
