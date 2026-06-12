using System;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Services;

public sealed class ExportService
{
    public void ExportFindingsCsv(string filePath)
    {
        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        // Trap Avoided: Exporting from UI Grid is banned. Always export directly from SQLite.
        cmd.CommandText = @"
            SELECT ScanId, DeviceId, CveId, Severity, Component, Status, ResolvedAt 
            FROM Findings
            ORDER BY DeviceId, Severity";

        using var reader = cmd.ExecuteReader();
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        writer.WriteLine("ScanId,DeviceId,CveId,Severity,Component,Status,ResolvedAt");

        while (reader.Read())
        {
            string scanId = reader.GetString(0);
            string deviceId = reader.GetString(1);
            string cveId = reader.GetString(2);
            string severity = reader.GetString(3);
            string component = EscapeCsv(reader.GetString(4));
            string status = reader.GetString(5);
            string resolvedAt = reader.IsDBNull(6) ? "" : reader.GetString(6);

            writer.WriteLine($"{scanId},{deviceId},{cveId},{severity},{component},{status},{resolvedAt}");
        }
    }

    private string EscapeCsv(string field)
    {
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
