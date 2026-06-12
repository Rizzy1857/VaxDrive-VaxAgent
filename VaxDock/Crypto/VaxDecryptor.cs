using System;
using System.Security.Cryptography;

namespace VaxDrive.VaxDock.Crypto;

public static class VaxDecryptor
{
    public static byte[] Decrypt(ReadOnlySpan<byte> fileBytes, byte[] aesKey)
    {
        // File layout: [VAX1(4)] [Nonce(12)] [Tag(16)] [Length(4)] [Ciphertext(N)] [HMAC(32)]
        // HmacVerifier must have run before calling this method.
        if (fileBytes.Length < 68) // 4+12+16+4+32
        {
            throw new CryptographicException("Payload too short to decrypt.");
        }

        ReadOnlySpan<byte> nonce = fileBytes.Slice(4, 12);
        ReadOnlySpan<byte> tag = fileBytes.Slice(16, 16);
        
        // Big-endian uint32 parsing
        uint ctLen = ((uint)fileBytes[32] << 24) |
                     ((uint)fileBytes[33] << 16) |
                     ((uint)fileBytes[34] << 8) |
                     ((uint)fileBytes[35]);

        ReadOnlySpan<byte> ciphertext = fileBytes.Slice(36, (int)ctLen);
        byte[] plaintext = new byte[ciphertext.Length];

        using (AesGcm aes = new AesGcm(aesKey, AesGcm.TagByteSizes.MaxSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return plaintext;
    }
}
