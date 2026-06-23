using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace VaxDrive.VaxDock.Data;

public sealed class FindingDto
{
    public int Id { get; init; }
    public string CveId { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Component { get; init; } = "";
    public string Status { get; init; } = "";
    public string? RemediationId { get; init; }
    public string? ResolvedAt { get; init; }
    public int Suppressed { get; init; }
    public string? SuppressReason { get; init; }
    public string? IgnoredUntil { get; init; }
}

public sealed class FindingRepository
{
    public IReadOnlyList<FindingDto> GetByDevice(string deviceId)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CveId, Severity, Component, Status, RemediationId, ResolvedAt, Suppressed, SuppressReason, IgnoredUntil
            FROM Findings
            WHERE DeviceId = @deviceId";
        cmd.Parameters.AddWithValue("@deviceId", deviceId);
        
        List<FindingDto> results = new List<FindingDto>();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new FindingDto
            {
                Id = reader.GetInt32(0),
                CveId = reader.GetString(1),
                Severity = reader.GetString(2),
                Component = reader.GetString(3),
                Status = reader.GetString(4),
                RemediationId = reader.IsDBNull(5) ? null : reader.GetString(5),
                ResolvedAt = reader.IsDBNull(6) ? null : reader.GetString(6),
                Suppressed = reader.GetInt32(7),
                SuppressReason = reader.IsDBNull(8) ? null : reader.GetString(8),
                IgnoredUntil = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }
        return results;
    }

    public void SuppressFinding(int findingId, string reason, int days)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Findings 
            SET Suppressed = 1, 
                SuppressReason = @reason, 
                IgnoredUntil = @ignoredUntil
            WHERE Id = @findingId";
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@ignoredUntil", DateTime.UtcNow.AddDays(days).ToString("O"));
        cmd.Parameters.AddWithValue("@findingId", findingId);
        cmd.ExecuteNonQuery();
    }
}