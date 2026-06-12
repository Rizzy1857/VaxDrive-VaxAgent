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
}

public sealed class FindingRepository
{
    public IReadOnlyList<FindingDto> GetByDevice(string deviceId)
    {
        SqliteConnection conn = DatabaseBootstrap.GetConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, CveId, Severity, Component, Status, RemediationId, ResolvedAt
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
                ResolvedAt = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return results;
    }
}
