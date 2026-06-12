using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VaxDrive.Models;
using VaxDrive.VaxDock.Crypto;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Services;

public sealed class IngestPipeline
{
    private readonly ScanRepository _scanRepo;
    private readonly QuarantineService _quarantine;

    public IngestPipeline(ScanRepository scanRepo, QuarantineService quarantine)
    {
        _scanRepo = scanRepo;
        _quarantine = quarantine;
    }

    public async Task RunAsync(string drivePath, byte[] hmacKey, byte[] aesKey)
    {
        string resultsPath = Path.Combine(drivePath, "results");
        if (!Directory.Exists(resultsPath)) return;

        string[] files = Directory.GetFiles(resultsPath, "*.vax");

        foreach (string file in files)
        {
            try
            {
                byte[] fileBytes;
                try
                {
                    fileBytes = await File.ReadAllBytesAsync(file);
                }
                catch (DirectoryNotFoundException ex)
                {
                    // Drive yanked mid-ingest — handle DriveNotFoundException gracefully
                    _quarantine.Quarantine(Path.GetFileName(file), "Drive yanked: " + ex.Message);
                    return; 
                }
                catch (IOException ex)
                {
                    _quarantine.Quarantine(Path.GetFileName(file), "IO Error reading file: " + ex.Message);
                    return;
                }

                // Strict linear state machine: Detected → Decrypted → HMACVerified → Parsed → Inserted. 
                // Wait, architecture states: Detected → HMACVerified → Decrypted → Parsed → Inserted.
                
                // Verify
                HmacVerifier.Verify(fileBytes, hmacKey);

                // Decrypt
                byte[] jsonBytes = VaxDecryptor.Decrypt(fileBytes, aesKey);

                // Parse
                ScanResult? result = JsonSerializer.Deserialize<ScanResult>(jsonBytes);
                if (result == null || string.IsNullOrEmpty(result.ScanId))
                {
                    throw new Exception("JSON payload invalid or missing ScanId");
                }

                // Insert (Transaction handled safely inside Upsert)
                _scanRepo.Upsert(result, System.Text.Encoding.UTF8.GetString(jsonBytes));
            }
            catch (HmacVerificationException ex)
            {
                _quarantine.Quarantine(Path.GetFileName(file), ex.Message);
            }
            catch (Exception ex)
            {
                _quarantine.Quarantine(Path.GetFileName(file), "Ingest failure: " + ex.Message);
            }
        }
    }
}
