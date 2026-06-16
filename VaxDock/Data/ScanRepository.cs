using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using VaxDrive.Models;

namespace VaxDrive.VaxDock.Data;

public sealed class ScanRepository
{
    public void Upsert(ScanResult scan, string rawJson)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteTransaction transaction = conn.BeginTransaction();
        try
        {
            // Insert Device if not exists
            using (SqliteCommand cmdDev = conn.CreateCommand())
            {
                cmdDev.Transaction = transaction;
                cmdDev.CommandText = @"
                    INSERT INTO Devices (Id, LastSeen, OsVersion)
                    VALUES (@id, @lastSeen, @osVersion)
                    ON CONFLICT(Id) DO UPDATE SET 
                        LastSeen = excluded.LastSeen,
                        OsVersion = excluded.OsVersion;";
                cmdDev.Parameters.AddWithValue("@id", scan.DeviceFingerprint);
                cmdDev.Parameters.AddWithValue("@lastSeen", scan.Timestamp.ToString("O"));
                cmdDev.Parameters.AddWithValue("@osVersion", scan.Os ?? (object)DBNull.Value);
                cmdDev.ExecuteNonQuery();
            }

            // Insert Scan
            using (SqliteCommand cmdScan = conn.CreateCommand())
            {
                cmdScan.Transaction = transaction;
                cmdScan.CommandText = @"
                    INSERT INTO Scans (ScanId, DeviceId, Timestamp, PatchLevel, RawJson, IngestedAt)
                    VALUES (@scanId, @deviceId, @timestamp, @patchLevel, @rawJson, @ingestedAt)
                    ON CONFLICT(ScanId) DO NOTHING;";
                cmdScan.Parameters.AddWithValue("@scanId", scan.ScanId);
                cmdScan.Parameters.AddWithValue("@deviceId", scan.DeviceFingerprint);
                cmdScan.Parameters.AddWithValue("@timestamp", scan.Timestamp.ToString("O"));
                cmdScan.Parameters.AddWithValue("@patchLevel", scan.PatchLevel ?? (object)DBNull.Value);
                cmdScan.Parameters.AddWithValue("@rawJson", rawJson);
                cmdScan.Parameters.AddWithValue("@ingestedAt", DateTime.UtcNow.ToString("O"));
                
                int affected = cmdScan.ExecuteNonQuery();
                if (affected == 0) 
                {
                    // Scan already exists. Skip insert to prevent duplicate finding entries.
                    transaction.Rollback();
                    return;
                }
            }

            // Insert Findings
            if (scan.Findings != null)
            {
                foreach (Finding f in scan.Findings)
                {
                    using SqliteCommand cmdFind = conn.CreateCommand();
                    cmdFind.Transaction = transaction;
                    cmdFind.CommandText = @"
                        INSERT INTO Findings (ScanId, DeviceId, CveId, Severity, Component, Status, RemediationId, DefinitionsPackVersion)
                        VALUES (@scanId, @deviceId, @cveId, @severity, @component, @status, @remId, @defPackVersion);";
                    cmdFind.Parameters.AddWithValue("@scanId", scan.ScanId);
                    cmdFind.Parameters.AddWithValue("@deviceId", scan.DeviceFingerprint);
                    cmdFind.Parameters.AddWithValue("@cveId", f.Id);
                    cmdFind.Parameters.AddWithValue("@severity", f.Severity);
                    cmdFind.Parameters.AddWithValue("@component", f.Component);
                    cmdFind.Parameters.AddWithValue("@status", f.Status);
                    cmdFind.Parameters.AddWithValue("@remId", f.RemediationId ?? (object)DBNull.Value);
                    cmdFind.Parameters.AddWithValue("@defPackVersion", scan.DefinitionsPackVersion);
                    cmdFind.ExecuteNonQuery();
                }
            }

            // Insert PlcNeighbors
            if (scan.PlcNeighbors != null)
            {
                foreach (PlcNeighbor plc in scan.PlcNeighbors)
                {
                    using SqliteCommand cmdPlc = conn.CreateCommand();
                    cmdPlc.Transaction = transaction;
                    cmdPlc.CommandText = @"
                        INSERT INTO PlcNeighbors (ScanId, Ip, Banner)
                        VALUES (@scanId, @ip, @banner);";
                    cmdPlc.Parameters.AddWithValue("@scanId", scan.ScanId);
                    cmdPlc.Parameters.AddWithValue("@ip", plc.Ip);
                    cmdPlc.Parameters.AddWithValue("@banner", plc.Banner ?? (object)DBNull.Value);
                    cmdPlc.ExecuteNonQuery();
                }
            }

            // Insert UsbAnomalies
            if (scan.UsbAnomalies != null)
            {
                foreach (string usb in scan.UsbAnomalies)
                {
                    using SqliteCommand cmdUsb = conn.CreateCommand();
                    cmdUsb.Transaction = transaction;
                    cmdUsb.CommandText = @"
                        INSERT INTO UsbAnomalies (ScanId, DeviceId, Description)
                        VALUES (@scanId, @deviceId, @desc);";
                    cmdUsb.Parameters.AddWithValue("@scanId", scan.ScanId);
                    cmdUsb.Parameters.AddWithValue("@deviceId", scan.DeviceFingerprint);
                    cmdUsb.Parameters.AddWithValue("@desc", usb);
                    cmdUsb.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public System.Collections.Generic.IReadOnlyList<ScanSummary> GetScanHistory(string deviceId)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.ScanId, s.Timestamp,
                   COUNT(CASE WHEN f.Severity = 'CRITICAL' AND f.ResolvedAt IS NULL THEN 1 END) AS CriticalCount,
                   COUNT(CASE WHEN f.ResolvedAt IS NOT NULL THEN 1 END) AS ResolvedCount
            FROM Scans s
            LEFT JOIN Findings f ON f.ScanId = s.ScanId
            WHERE s.DeviceId = @deviceId
            GROUP BY s.ScanId, s.Timestamp
            ORDER BY s.Timestamp ASC";
        cmd.Parameters.AddWithValue("@deviceId", deviceId);

        var results = new System.Collections.Generic.List<ScanSummary>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ScanSummary
            {
                ScanId = reader.GetString(0),
                Timestamp = reader.GetString(1),
                CriticalCount = reader.GetInt32(2),
                ResolvedCount = reader.GetInt32(3)
            });
        }
        return results;
    }
}

public sealed class ScanSummary
{
    public string ScanId { get; init; } = "";
    public string Timestamp { get; init; } = "";
    public int CriticalCount { get; init; }
    public int ResolvedCount { get; init; }
}
