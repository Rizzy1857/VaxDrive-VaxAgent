#if NET35
using System;
using System.IO;
using System.Security.Cryptography;

namespace VaxDrive.VaxAgent.Crypto;

public static class LegacyEncryptor
{
    private const int IvSize = 16;
    private const int MacSize = 32; // HMAC-SHA256 is 32 bytes
    private const int KeySize = 32; // 256-bit key
    private const int SaltSize = 16;
    private const int Iterations = 10000;

    public static byte[] Encrypt(byte[] plaintext, byte[] passwordKey)
    {
        if (plaintext == null || plaintext.Length == 0) throw new ArgumentException("Plaintext empty");
        if (passwordKey == null || passwordKey.Length == 0) throw new ArgumentException("Key empty");

        // Derive keys using PBKDF2-SHA1 (Rfc2898DeriveBytes available in .NET 3.5)
        byte[] salt = new byte[SaltSize];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(salt);
        }

        using (var deriveBytes = new Rfc2898DeriveBytes(passwordKey, salt, Iterations))
        {
            byte[] encKey = deriveBytes.GetBytes(KeySize);
            byte[] macKey = deriveBytes.GetBytes(KeySize);

            try
            {
                using (var aes = new AesCryptoServiceProvider())
                {
                    aes.Key = encKey;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    byte[] iv = new byte[IvSize];
                    using (var rng = new RNGCryptoServiceProvider())
                    {
                        rng.GetBytes(iv);
                    }
                    aes.IV = iv;

                    byte[] ciphertext;
                    using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            cs.Write(plaintext, 0, plaintext.Length);
                            cs.FlushFinalBlock();
                        }
                        ciphertext = ms.ToArray();
                    }

                    // Compute HMAC-SHA256 over IV + Ciphertext
                    byte[] ivAndCipher = new byte[iv.Length + ciphertext.Length];
                    Buffer.BlockCopy(iv, 0, ivAndCipher, 0, iv.Length);
                    Buffer.BlockCopy(ciphertext, 0, ivAndCipher, iv.Length, ciphertext.Length);

                    byte[] mac;
                    using (var hmac = new HMACSHA256(macKey))
                    {
                        mac = hmac.ComputeHash(ivAndCipher);
                    }

                    // Final payload: Salt(16) + IV(16) + MAC(32) + Ciphertext(N)
                    byte[] result = new byte[salt.Length + iv.Length + mac.Length + ciphertext.Length];
                    Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
                    Buffer.BlockCopy(iv, 0, result, salt.Length, iv.Length);
                    Buffer.BlockCopy(mac, 0, result, salt.Length + iv.Length, mac.Length);
                    Buffer.BlockCopy(ciphertext, 0, result, salt.Length + iv.Length + mac.Length, ciphertext.Length);

                    return result;
                }
            }
            finally
            {
                Array.Clear(encKey, 0, encKey.Length);
                Array.Clear(macKey, 0, macKey.Length);
            }
        }
    }

    public static byte[] Decrypt(byte[] payload, byte[] passwordKey)
    {
        if (payload == null || payload.Length < SaltSize + IvSize + MacSize)
            throw new ArgumentException("Payload too short");
        if (passwordKey == null || passwordKey.Length == 0)
            throw new ArgumentException("Key empty");

        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[IvSize];
        byte[] expectedMac = new byte[MacSize];
        byte[] ciphertext = new byte[payload.Length - (SaltSize + IvSize + MacSize)];

        Buffer.BlockCopy(payload, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(payload, SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(payload, SaltSize + IvSize, expectedMac, 0, MacSize);
        Buffer.BlockCopy(payload, SaltSize + IvSize + MacSize, ciphertext, 0, ciphertext.Length);

        using (var deriveBytes = new Rfc2898DeriveBytes(passwordKey, salt, Iterations))
        {
            byte[] encKey = deriveBytes.GetBytes(KeySize);
            byte[] macKey = deriveBytes.GetBytes(KeySize);

            try
            {
                // Reconstruct IV + Ciphertext to verify MAC
                byte[] ivAndCipher = new byte[iv.Length + ciphertext.Length];
                Buffer.BlockCopy(iv, 0, ivAndCipher, 0, iv.Length);
                Buffer.BlockCopy(ciphertext, 0, ivAndCipher, iv.Length, ciphertext.Length);

                using (var hmac = new HMACSHA256(macKey))
                {
                    byte[] actualMac = hmac.ComputeHash(ivAndCipher);
                    if (!ConstantTimeEquals(expectedMac, actualMac))
                    {
                        throw new CryptographicException("HMAC mismatch. Payload tampered or invalid key.");
                    }
                }

                using (var aes = new AesCryptoServiceProvider())
                {
                    aes.Key = encKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    using (var ms = new MemoryStream(ciphertext))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var dest = new MemoryStream())
                    {
                        int b;
                        while ((b = cs.ReadByte()) != -1)
                        {
                            dest.WriteByte((byte)b);
                        }
                        return dest.ToArray();
                    }
                }
            }
            finally
            {
                Array.Clear(encKey, 0, encKey.Length);
                Array.Clear(macKey, 0, macKey.Length);
            }
        }
    }

    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }
}
#endif
