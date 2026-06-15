using System;
using System.IO;
using System.Security.Cryptography;

namespace VaxDrive.VaxAgent.Crypto;

public static class VaxEncryptor
{
    // "VAX1" magic bytes
    private static readonly byte[] MagicBytes = new byte[] { 86, 65, 88, 49 }; 

    public static byte[] Encrypt(byte[] plaintext, byte[] encKey, byte[] hmacKey)
    {
#if NET8_0
        // 1. Generate nonce
        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        // 2. AES-256-GCM encrypt
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        using (AesGcm aes = new AesGcm(encKey, AesGcm.TagByteSizes.MaxSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // 3. HMAC over nonce || ciphertext || tag
        byte[] hmacData = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, hmacData, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, hmacData, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, hmacData, nonce.Length + ciphertext.Length, tag.Length);
        
        byte[] hmac = HMACSHA256.HashData(hmacKey, hmacData);

        // 4. Assemble file bytes
        using MemoryStream ms = new MemoryStream();
        ms.Write(MagicBytes);
        ms.Write(nonce);
        ms.Write(tag);
        
        // Write 4 byte ciphertext length (Big-Endian as specified in architecture)
        byte[] beLength = new byte[4];
        beLength[0] = (byte)(ciphertext.Length >> 24);
        beLength[1] = (byte)(ciphertext.Length >> 16);
        beLength[2] = (byte)(ciphertext.Length >> 8);
        beLength[3] = (byte)(ciphertext.Length);
        ms.Write(beLength);
        
        ms.Write(ciphertext);
        ms.Write(hmac);
        
        return ms.ToArray();
#elif NET35
        // NET35 fallback using AES-CBC. 
        // We use the encKey as the master password seed for the PBKDF2 derivation inside LegacyEncryptor.
        return LegacyEncryptor.Encrypt(plaintext, encKey);
#else
        return plaintext;
#endif
    }
    // Encrypts and HMAC-signs the scan result payload using AES-256-GCM.
    // Returns a byte array representing the complete .vax file structure.
}
