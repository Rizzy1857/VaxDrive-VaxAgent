using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace VaxDrive.VaxDock.Data;

public class CveRecord
{
    public string CveId { get; set; } = string.Empty;
    public string Published { get; set; } = string.Empty;
    public string Modified { get; set; } = string.Empty;
    public double CvssV3 { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CpeList { get; set; } = string.Empty;
}

public class DeltaRecord
{
    public string CveId { get; set; } = string.Empty;
    public double OldScore { get; set; }
    public double NewScore { get; set; }
    public string RecordedAt { get; set; } = string.Empty;
}

public class CveRepository
{
    private readonly string _connectionString;
    private readonly string _dbKey;

    public CveRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        _dbKey = Environment.GetEnvironmentVariable("VAXDRIVE_DB_KEY") ?? string.Empty;
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        
        using var cmdWal = conn.CreateCommand();
        cmdWal.CommandText = "PRAGMA journal_mode = WAL;";
        cmdWal.ExecuteNonQuery();

        if (!string.IsNullOrEmpty(_dbKey))
        {
            using var cmdKey = conn.CreateCommand();
            cmdKey.CommandText = $"PRAGMA key = '{_dbKey.Replace("'", "''")}';";
            cmdKey.ExecuteNonQuery();
        }

        return conn;
    }

    public void UpsertCve(CveRecord r)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cve_cache (cve_id, published, modified, cvss_v3, description, cpe_list)
            VALUES (@cveId, @published, @modified, @cvssV3, @description, @cpeList)
            ON CONFLICT(cve_id) DO UPDATE SET
                modified = excluded.modified,
                cvss_v3 = excluded.cvss_v3,
                description = excluded.description,
                cpe_list = excluded.cpe_list;";

        cmd.Parameters.AddWithValue("@cveId", r.CveId);
        cmd.Parameters.AddWithValue("@published", r.Published);
        cmd.Parameters.AddWithValue("@modified", r.Modified);
        cmd.Parameters.AddWithValue("@cvssV3", r.CvssV3);
        cmd.Parameters.AddWithValue("@description", r.Description);
        cmd.Parameters.AddWithValue("@cpeList", r.CpeList);
        cmd.ExecuteNonQuery();
    }

    public List<CveRecord> SearchByCpe(string cpe)
    {
        var results = new List<CveRecord>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        
        // Escape % and _ in input
        string escapedCpe = cpe.Replace("%", "\\%").Replace("_", "\\_");
        
        cmd.CommandText = @"
            SELECT cve_id, published, modified, cvss_v3, description, cpe_list 
            FROM cve_cache 
            WHERE cpe_list LIKE @cpe ESCAPE '\';";
            
        cmd.Parameters.AddWithValue("@cpe", "%" + escapedCpe + "%");
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new CveRecord
            {
                CveId = reader.GetString(0),
                Published = reader.GetString(1),
                Modified = reader.GetString(2),
                CvssV3 = reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3),
                Description = reader.IsDBNull(4) ? "" : reader.GetString(4),
                CpeList = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }
        return results;
    }

    public List<DeltaRecord> GetDelta(DateTime since)
    {
        var results = new List<DeltaRecord>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT cve_id, old_score, new_score, recorded_at 
            FROM severity_deltas 
            WHERE recorded_at > @since;";
            
        cmd.Parameters.AddWithValue("@since", since.ToString("O"));
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new DeltaRecord
            {
                CveId = reader.GetString(0),
                OldScore = reader.GetDouble(1),
                NewScore = reader.GetDouble(2),
                RecordedAt = reader.GetString(3)
            });
        }
        return results;
    }

    public void StoreSeverityDelta(string cveId, double oldScore, double newScore)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO severity_deltas (cve_id, old_score, new_score, recorded_at)
            VALUES (@cveId, @oldScore, @newScore, @recordedAt);";
            
        cmd.Parameters.AddWithValue("@cveId", cveId);
        cmd.Parameters.AddWithValue("@oldScore", oldScore);
        cmd.Parameters.AddWithValue("@newScore", newScore);
        cmd.Parameters.AddWithValue("@recordedAt", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public int GetLastCheckpointStartIndex()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_start_index FROM sync_checkpoints WHERE id = 1;";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public void UpsertCheckpoint(int startIndex, string eTagOrStatus)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO sync_checkpoints (id, last_start_index, last_etag)
            VALUES (1, @startIndex, @eTag)
            ON CONFLICT(id) DO UPDATE SET
                last_start_index = excluded.last_start_index,
                last_etag = excluded.last_etag;";
                
        cmd.Parameters.AddWithValue("@startIndex", startIndex);
        cmd.Parameters.AddWithValue("@eTag", eTagOrStatus);
        cmd.ExecuteNonQuery();
    }

    public void StorePageHash(int startIndex, string hashHex)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS page_hashes (start_index INTEGER PRIMARY KEY, hash_hex TEXT);
            INSERT INTO page_hashes (start_index, hash_hex) VALUES (@startIndex, @hashHex)
            ON CONFLICT(start_index) DO UPDATE SET hash_hex = excluded.hash_hex;";
            
        cmd.Parameters.AddWithValue("@startIndex", startIndex);
        cmd.Parameters.AddWithValue("@hashHex", hashHex);
        cmd.ExecuteNonQuery();
    }
}
