#if NET35
using System;
using System.Security.Cryptography;
using Xunit;
using VaxDrive.VaxAgent.Crypto;

namespace VaxDrive.VaxAgent.Tests.Crypto;

public class LegacyEncryptorTests
{
    private readonly byte[] _key = System.Text.Encoding.UTF8.GetBytes("SuperSecretPasswordKey32Bytes!!!");
    private readonly byte[] _wrongKey = System.Text.Encoding.UTF8.GetBytes("WrongSecretPasswordKey32Bytes!!!");
    private readonly byte[] _plaintext = System.Text.Encoding.UTF8.GetBytes("Hello VaxDrive OT!");

    [Fact]
    public void EncryptDecrypt_Roundtrip_Succeeds()
    {
        byte[] ciphertext = LegacyEncryptor.Encrypt(_plaintext, _key);
        byte[] decrypted = LegacyEncryptor.Decrypt(ciphertext, _key);

        Assert.Equal(_plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        byte[] ciphertext = LegacyEncryptor.Encrypt(_plaintext, _key);
        
        // Tamper with the MAC (MAC starts at offset 32: 16 byte salt + 16 byte IV)
        ciphertext[35] ^= 0xFF;

        Assert.Throws<CryptographicException>(() => LegacyEncryptor.Decrypt(ciphertext, _key));
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsCryptographicException()
    {
        byte[] ciphertext = LegacyEncryptor.Encrypt(_plaintext, _key);

        Assert.Throws<CryptographicException>(() => LegacyEncryptor.Decrypt(ciphertext, _wrongKey));
    }

    [Fact]
    public void Encrypt_RandomIV_ProducesDifferentCiphertexts()
    {
        byte[] ciphertext1 = LegacyEncryptor.Encrypt(_plaintext, _key);
        byte[] ciphertext2 = LegacyEncryptor.Encrypt(_plaintext, _key);

        // They must differ because of random salt and random IV
        Assert.NotEqual(ciphertext1, ciphertext2);
    }
}
#endif
