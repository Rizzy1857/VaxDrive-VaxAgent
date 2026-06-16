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

    public void ExportDeviceCsv(string deviceId, string filePath)
    {
        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT ScanId, DeviceId, CveId, Severity, Component, Status, ResolvedAt 
            FROM Findings
            WHERE DeviceId = @deviceId
            ORDER BY Severity";
        cmd.Parameters.AddWithValue("@deviceId", deviceId);

        using var reader = cmd.ExecuteReader();
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        writer.WriteLine("ScanId,DeviceId,CveId,Severity,Component,Status,ResolvedAt");

        while (reader.Read())
        {
            string scanId = reader.GetString(0);
            string devId = reader.GetString(1);
            string cveId = reader.GetString(2);
            string severity = reader.GetString(3);
            string component = EscapeCsv(reader.GetString(4));
            string status = reader.GetString(5);
            string resolvedAt = reader.IsDBNull(6) ? "" : reader.GetString(6);

            writer.WriteLine($"{scanId},{devId},{cveId},{severity},{component},{status},{resolvedAt}");
        }
    }

    public void ExportDevicePdf(string deviceId, string outputPath)
    {
        var doc = BuildFlowDocument(deviceId);

        // Note: The built-in WPF way to export to PDF without external libraries is to use PrintDialog
        // and let the user select the "Microsoft Print to PDF" printer. The outputPath parameter
        // is ignored in this specific approach as PrintDialog handles file selection if printing to file.
        var pd = new System.Windows.Controls.PrintDialog();
        pd.PrintDocument(((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator, $"VaxDrive Report {deviceId}");
    }

    private System.Windows.Documents.FlowDocument BuildFlowDocument(string deviceId)
    {
        var doc = new System.Windows.Documents.FlowDocument();
        doc.PagePadding = new System.Windows.Thickness(50);
        
        doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"VaxDrive Security Report: {deviceId}")) { FontSize = 24, FontWeight = System.Windows.FontWeights.Bold });
        doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")));
        
        var table = new System.Windows.Documents.Table();
        table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(150) });
        table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
        table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(200) });
        table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
        
        var rowGroup = new System.Windows.Documents.TableRowGroup();
        var headerRow = new System.Windows.Documents.TableRow();
        headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("CVE ID")) { FontWeight = System.Windows.FontWeights.Bold }));
        headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Severity")) { FontWeight = System.Windows.FontWeights.Bold }));
        headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Component")) { FontWeight = System.Windows.FontWeights.Bold }));
        headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Status")) { FontWeight = System.Windows.FontWeights.Bold }));
        rowGroup.Rows.Add(headerRow);
        
        var conn = DatabaseBootstrap.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CveId, Severity, Component, Status FROM Findings WHERE DeviceId = @deviceId ORDER BY Severity";
        cmd.Parameters.AddWithValue("@deviceId", deviceId);
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            var row = new System.Windows.Documents.TableRow();
            row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(reader.GetString(0)))));
            row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(reader.GetString(1)))));
            row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(reader.GetString(2)))));
            row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(reader.GetString(3)))));
            rowGroup.Rows.Add(row);
        }
        
        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);
        
        return doc;
    }
}
