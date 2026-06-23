using System;
using System.IO;
using System.Security;
#if NET8_0
using System.Text.Json;
#endif
using VaxDrive.Models;
using VaxDrive.VaxAgent.Crypto;

namespace VaxDrive.VaxAgent.Output;

public sealed class VaxFileWriter
{
    public void Write(ScanResult result, string resultsDirPath, byte[] encKey, byte[] hmacKey)
    {
#if NET8_0
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(result);
        byte[] encrypted = VaxEncryptor.Encrypt(json, encKey, hmacKey);

        string folderName = result.Timestamp.ToString("yyyy-MM-dd");
        string filename = $"{result.Timestamp:yyyyMMddTHHmmssZ}_{result.DeviceFingerprint}.vax";
        string targetDir = Path.Combine(resultsDirPath, folderName);
        Directory.CreateDirectory(targetDir);
        string fullPath = Path.Combine(targetDir, filename);
        
        // Convert to absolute paths to prevent path traversal tricks
        string absResultDir = Path.GetFullPath(resultsDirPath);
        string absFullPath = Path.GetFullPath(fullPath);

        if (!absFullPath.StartsWith(absResultDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException("Output path must be on drive — host write blocked.");
        }

        File.WriteAllBytes(absFullPath, encrypted);
#else
        // TODO: Net35 implementation requires alternative JSON serializer
#endif
    }
    // Serializes the ScanResult to JSON, encrypts it, and writes the .vax file safely to the drive's /results directory.
    // Returns void. Throws SecurityException if path traversal is attempted.
}
