using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;
using VaxDrive.VaxDock.Crypto;

namespace VaxDrive.VaxDock.Tests.Crypto;

public sealed class HmacVerifierTests
{
    private readonly byte[] _hmacKey = new byte[32];
    
    public HmacVerifierTests()
    {
        RandomNumberGenerator.Fill(_hmacKey);
    }

    private byte[] CreateValidVaxFile()
    {
        byte[] nonce = new byte[12];
        byte[] tag = new byte[16];
        byte[] ciphertext = new byte[] { 0xAA, 0xBB, 0xCC }; // 3 bytes
        
        // Proper HMAC data construction matching the writer: nonce || ciphertext || tag
        byte[] hmacData = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, hmacData, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, hmacData, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, hmacData, nonce.Length + ciphertext.Length, tag.Length);
        
        byte[] hmac = HMACSHA256.HashData(_hmacKey, hmacData);

        using MemoryStream ms = new MemoryStream();
        ms.Write("VAX1"u8);
        ms.Write(nonce);
        ms.Write(tag);
        
        // 4 byte ciphertext length (Big-Endian)
        byte[] beLength = new byte[4];
        beLength[0] = (byte)(ciphertext.Length >> 24);
        beLength[1] = (byte)(ciphertext.Length >> 16);
        beLength[2] = (byte)(ciphertext.Length >> 8);
        beLength[3] = (byte)(ciphertext.Length);
        ms.Write(beLength);
        
        ms.Write(ciphertext);
        ms.Write(hmac);
        
        return ms.ToArray();
    }

    [Fact]
    public void Verify_ValidFile_DoesNotThrow()
    {
        byte[] validFile = CreateValidVaxFile();
        HmacVerifier.Verify(validFile, _hmacKey);
        // implicit assert: does not throw HmacVerificationException
    }

    [Fact]
    public void Verify_TamperedHmac_ThrowsException()
    {
        byte[] tamperedFile = CreateValidVaxFile();
        tamperedFile[^1] ^= 0xFF; // Flip a bit in the HMAC

        Assert.Throws<HmacVerificationException>(() => HmacVerifier.Verify(tamperedFile, _hmacKey));
    }

    [Fact]
    public void Verify_MalformedInput_FileTooShort_ThrowsException()
    {
        byte[] shortFile = new byte[10];
        Assert.Throws<HmacVerificationException>(() => HmacVerifier.Verify(shortFile, _hmacKey));
    }
}
