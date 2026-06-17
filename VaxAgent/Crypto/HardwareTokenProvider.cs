using System.IO;

namespace VaxDrive.VaxAgent.Crypto;

public static class HardwareTokenProvider
{
    public static byte[] GetTokenBytes(string drivePath)
    {
        string tokenPath = Path.Combine(drivePath, "boot", "device.token");
        
        // TODO: OPEN-1 - Production should use IronKey/Kingston SDK API to get hardware-bound token
        if (!File.Exists(tokenPath))
        {
            // Auto-provision a random hardware token on first run if missing
            byte[] newToken = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(newToken);
            }
            
            try
            {
                string? bootDir = Path.GetDirectoryName(tokenPath);
                if (bootDir != null && !Directory.Exists(bootDir)) Directory.CreateDirectory(bootDir);
                File.WriteAllBytes(tokenPath, newToken);
                System.Console.WriteLine($"[HMAC_AUDIT] {System.DateTime.UtcNow:O} | Provisioned new random hardware token at {tokenPath}");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[!] Error provisioning token: {ex.Message}");
                throw new System.InvalidOperationException("Cannot proceed without a hardware token.", ex);
            }
            
            return newToken;
        }
        
        return File.ReadAllBytes(tokenPath);
    }
    // STUB: Reads a deterministic 32-byte token from /boot/device.token.
    // Returns byte array representing the hardware token.
}
