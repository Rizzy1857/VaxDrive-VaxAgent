using System;
using System.Security.Cryptography;

namespace VaxDrive.VaxDock.Crypto;

public static class HmacVerifier
{
    public static void Verify(ReadOnlySpan<byte> fileBytes, byte[] hmacKey)
    {
        // Extract components
        if (fileBytes.Length < 4 + 12 + 16 + 4 + 32)
        {
            throw new HmacVerificationException("File too short to be valid .vax");
        }

        ReadOnlySpan<byte> magic = fileBytes.Slice(0, 4);
        if (!magic.SequenceEqual("VAX1"u8))
        {
            throw new HmacVerificationException("Invalid magic bytes");
        }

        ReadOnlySpan<byte> nonce = fileBytes.Slice(4, 12);
        ReadOnlySpan<byte> tag = fileBytes.Slice(16, 16);
        
        // Big-endian uint32 parsing for ciphertext length
        uint ctLen = ((uint)fileBytes[32] << 24) |
                     ((uint)fileBytes[33] << 16) |
                     ((uint)fileBytes[34] << 8) |
                     ((uint)fileBytes[35]);

        if (fileBytes.Length != 36 + ctLen + 32)
        {
            throw new HmacVerificationException("File length does not match payload size");
        }

        ReadOnlySpan<byte> ciphertext = fileBytes.Slice(36, (int)ctLen);
        ReadOnlySpan<byte> storedHmac = fileBytes.Slice(36 + (int)ctLen, 32);

        // Recompute HMAC over nonce || ciphertext || tag
        byte[] hmacData = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(hmacData.AsSpan());
        ciphertext.CopyTo(hmacData.AsSpan(nonce.Length));
        tag.CopyTo(hmacData.AsSpan(nonce.Length + ciphertext.Length));

        byte[] computed = HMACSHA256.HashData(hmacKey, hmacData);

        // Constant-time compare
        if (!CryptographicOperations.FixedTimeEquals(computed, storedHmac))
        {
            throw new HmacVerificationException("HMAC mismatch — file tampered or corrupt");
        }
    }
}

public sealed class HmacVerificationException : Exception
{
    public HmacVerificationException(string message) : base(message) { }
}
