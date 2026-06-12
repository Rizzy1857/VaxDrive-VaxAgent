using System.Security.Cryptography;

namespace VaxDrive.VaxAgent.Crypto;

public static class KeyDerivation
{
    public static byte[] DeriveAesKey(byte[] hardwareToken)
    {
#if NET8_0
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, hardwareToken, 32, System.Text.Encoding.UTF8.GetBytes("VAXDRIVE-V1"));
#else
        // TODO: Net35 HKDF manual implementation
        return new byte[32];
#endif
    }
    // Derives a 32-byte AES-256-GCM key from the hardware token using HKDF-SHA256.
    // Returns a 32-byte array.

    public static byte[] DeriveHmacKey(byte[] hardwareToken)
    {
#if NET8_0
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, hardwareToken, 32, System.Text.Encoding.UTF8.GetBytes("VAXDRIVE-HMAC-V1"));
#else
        // TODO: Net35 HKDF manual implementation
        return new byte[32];
#endif
    }
    // Derives a 32-byte HMAC-SHA256 key from the hardware token using HKDF-SHA256.
    // Returns a 32-byte array.
}
