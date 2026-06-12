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
            // Fallback for dev - return a deterministic 32-byte key if no token file exists
            return new byte[32]; 
        }
        
        return File.ReadAllBytes(tokenPath);
    }
    // STUB: Reads a deterministic 32-byte token from /boot/device.token.
    // Returns byte array representing the hardware token.
}
