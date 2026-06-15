using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;
using VaxDrive.VaxDock.Crypto;

namespace VaxDrive.VaxDock.Tests.Crypto;

public sealed class VaxDecryptorTests
{
    private readonly byte[] _aesKey = new byte[32];
    
    public VaxDecryptorTests()
    {
        RandomNumberGenerator.Fill(_aesKey);
    }

    private byte[] CreateEncryptedPayload(byte[] plaintext)
    {
        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        
        using (AesGcm aes = new AesGcm(_aesKey, AesGcm.TagByteSizes.MaxSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        using MemoryStream ms = new MemoryStream();
        ms.Write("VAX1"u8);
        ms.Write(nonce);
        ms.Write(tag);
        
        byte[] beLength = new byte[4];
        beLength[0] = (byte)(ciphertext.Length >> 24);
        beLength[1] = (byte)(ciphertext.Length >> 16);
        beLength[2] = (byte)(ciphertext.Length >> 8);
        beLength[3] = (byte)(ciphertext.Length);
        ms.Write(beLength);
        
        ms.Write(ciphertext);
        
        byte[] dummyHmac = new byte[32];
        ms.Write(dummyHmac);
        
        return ms.ToArray();
    }

    [Fact]
    public void Decrypt_ValidPayload_ReturnsPlaintext()
    {
        byte[] expected = "{\"test\":\"data\"}"u8.ToArray();
        byte[] payload = CreateEncryptedPayload(expected);

        byte[] actual = VaxDecryptor.Decrypt(payload, _aesKey);
        
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Decrypt_InvalidTag_ThrowsCryptographicException()
    {
        byte[] payload = CreateEncryptedPayload("data"u8.ToArray());
        payload[16] ^= 0xFF; // flip bit in tag

        Assert.ThrowsAny<CryptographicException>(() => VaxDecryptor.Decrypt(payload, _aesKey));
    }

    [Fact]
    public void Decrypt_MalformedFile_ThrowsCryptographicException()
    {
        byte[] shortFile = new byte[10];
        Assert.ThrowsAny<CryptographicException>(() => VaxDecryptor.Decrypt(shortFile, _aesKey));
    }
}
