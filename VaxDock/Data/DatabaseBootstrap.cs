using System;
using System.IO;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace VaxDrive.VaxDock.Data;

public static class DatabaseBootstrap
{
    private static SqliteConnection? _longLivedConnection;

    // Retrieves the single long-lived connection with WAL mode enabled
    public static SqliteConnection GetConnection()
    {
        if (_longLivedConnection == null)
            throw new InvalidOperationException("Database not initialized.");
        return _longLivedConnection;
    }

    public static void Initialize(string dbPath)
    {
        string connectionString = $"Data Source={dbPath}";
        _longLivedConnection = new SqliteConnection(connectionString);
        _longLivedConnection.Open();

        using SqliteCommand pragmaCmd = _longLivedConnection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
        pragmaCmd.ExecuteNonQuery();

        string schema;
        var assembly = Assembly.GetExecutingAssembly();
        using (Stream stream = assembly.GetManifestResourceStream("VaxDock.Data.SchemaV1.sql") ?? throw new InvalidOperationException("Could not find SchemaV1.sql embedded resource"))
        using (StreamReader reader = new StreamReader(stream))
        {
            schema = reader.ReadToEnd();
        }

        using SqliteCommand command = _longLivedConnection.CreateCommand();
#pragma warning disable CA2100
        command.CommandText = schema;
#pragma warning restore CA2100
        command.ExecuteNonQuery();

        // Safe migrations for MVP
        try { new SqliteCommand("ALTER TABLE Devices ADD COLUMN AssetCriticality TEXT DEFAULT 'UNCLASSIFIED';", _longLivedConnection).ExecuteNonQuery(); } catch (SqliteException) { }
        try { new SqliteCommand("ALTER TABLE Scans ADD COLUMN Completeness TEXT;", _longLivedConnection).ExecuteNonQuery(); } catch (SqliteException) { }
        try { new SqliteCommand("ALTER TABLE Scans ADD COLUMN DefinitionsPackGenerated TEXT;", _longLivedConnection).ExecuteNonQuery(); } catch (SqliteException) { }
        try { new SqliteCommand("ALTER TABLE Findings ADD COLUMN Suppressed INTEGER DEFAULT 0;", _longLivedConnection).ExecuteNonQuery(); } catch (SqliteException) { }
        try { new SqliteCommand("ALTER TABLE Findings ADD COLUMN SuppressReason TEXT;", _longLivedConnection).ExecuteNonQuery(); } catch (SqliteException) { }
        try { new SqliteCommand("ALTER TABLE Findings ADD COLUMN IgnoredUntil TEXT;", _longLivedConnection).ExecuteNonQuery(); } catch (SqliteException) { }
    }
}
