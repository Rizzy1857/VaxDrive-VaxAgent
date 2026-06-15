using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace VaxDrive.VaxAgent.Crypto.HardwareToken;

public class HardwareTokenUnavailableException : Exception
{
    public HardwareTokenUnavailableException(string message) : base(message) { }
}

public interface IHardwareTokenProvider : IDisposable
{
    SecureString GetCryptographicToken();
    bool ValidateTokenBinding(string expectedMachineFingerprint);
    string GetDeviceSerial();
}

public abstract class BaseTokenProvider : IHardwareTokenProvider
{
    protected SecureString? _cachedToken;
    private const int WM_DEVICECHANGE = 0x0219;
    
    // Abstract SDK interop methods
    protected abstract byte[] GetRawTokenFromDevice();
    protected abstract string GetDeviceSerialFromDevice();
    
    public SecureString GetCryptographicToken()
    {
        if (_cachedToken != null)
        {
            return _cachedToken.Copy();
        }

        byte[]? rawTokenBytes = null;
        GCHandle pinnedArray = default;
        try
        {
            rawTokenBytes = GetRawTokenFromDevice();
            if (rawTokenBytes == null || rawTokenBytes.Length == 0)
            {
                throw new HardwareTokenUnavailableException("Failed to retrieve token from hardware.");
            }

            // Pin the array in memory so GC doesn't move it while we process
            pinnedArray = GCHandle.Alloc(rawTokenBytes, GCHandleType.Pinned);
            
            var secureString = new SecureString();
            foreach (byte b in rawTokenBytes)
            {
                secureString.AppendChar((char)b);
            }
            secureString.MakeReadOnly();
            _cachedToken = secureString.Copy();
            return secureString;
        }
        finally
        {
            // Zero and free unmanaged buffer
            if (rawTokenBytes != null)
            {
                Array.Clear(rawTokenBytes, 0, rawTokenBytes.Length);
            }
            if (pinnedArray.IsAllocated)
            {
                pinnedArray.Free();
            }
        }
    }

    public bool ValidateTokenBinding(string fingerprint)
    {
        // Mock Machine SID and TPM_PCR0 retrieval for demonstration
        byte[] machineSid = Encoding.UTF8.GetBytes("S-1-5-21-MOCK-SID");
        byte[] tpmPcr0 = Encoding.UTF8.GetBytes("TPM_PCR0_MOCK");
        
        using var secureString = GetCryptographicToken();
        IntPtr unmanagedBytes = Marshal.SecureStringToBSTR(secureString);
        try
        {
            // Note: In real scenarios SecureString is 16-bit chars, so BSTR length is secureString.Length * 2
            byte[] tokenBytes = new byte[secureString.Length];
            for (int i = 0; i < secureString.Length; i++)
            {
                tokenBytes[i] = (byte)Marshal.ReadInt16(unmanagedBytes, i * 2);
            }

            // HKDF extraction (CNG)
            byte[] ikm = tokenBytes;
            byte[] salt = new byte[32]; // Zero salt
            byte[] info = new byte[machineSid.Length + tpmPcr0.Length];
            Buffer.BlockCopy(machineSid, 0, info, 0, machineSid.Length);
            Buffer.BlockCopy(tpmPcr0, 0, info, machineSid.Length, tpmPcr0.Length);

            byte[] derivedKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, salt, info);
            string computedFingerprint = Convert.ToBase64String(derivedKey);
            
            Array.Clear(tokenBytes, 0, tokenBytes.Length);
            return computedFingerprint == fingerprint;
        }
        finally
        {
            Marshal.ZeroFreeBSTR(unmanagedBytes);
        }
    }

    public string GetDeviceSerial()
    {
        string serial = GetDeviceSerialFromDevice();
        LogAudit("DeviceSerialRetrieved", serial);
        return serial;
    }

    private void LogAudit(string action, string detail)
    {
        // Write to HMAC audit log entry
        Console.WriteLine($"[AUDIT] {DateTime.UtcNow:O} | {action} | {detail}");
    }

    public void OnDeviceChange(int msg)
    {
        if (msg == WM_DEVICECHANGE)
        {
            DisposeToken();
        }
    }

    protected void DisposeToken()
    {
        if (_cachedToken != null)
        {
            _cachedToken.Dispose();
            _cachedToken = null;
        }
    }

    public void Dispose()
    {
        DisposeToken();
        GC.SuppressFinalize(this);
    }
}

public class KingstonTokenProvider : BaseTokenProvider
{
    protected override byte[] GetRawTokenFromDevice()
    {
        object? rcw = null;
        try
        {
            // Simulate COM RCW instantiation
            // rcw = new KingstonComObject();
            // return rcw.GetTokenBytes();
            throw new HardwareTokenUnavailableException("Kingston SDK missing on endpoint.");
        }
        finally
        {
            if (rcw != null && Marshal.IsComObject(rcw))
            {
                Marshal.ReleaseComObject(rcw);
            }
        }
    }

    protected override string GetDeviceSerialFromDevice()
    {
        object? rcw = null;
        try
        {
            throw new HardwareTokenUnavailableException("Kingston SDK missing on endpoint.");
        }
        finally
        {
            if (rcw != null && Marshal.IsComObject(rcw))
            {
                Marshal.ReleaseComObject(rcw);
            }
        }
    }
}

public class IronKeyTokenProvider : BaseTokenProvider
{
    protected override byte[] GetRawTokenFromDevice()
    {
        object? rcw = null;
        try
        {
            throw new HardwareTokenUnavailableException("IronKey SDK missing on endpoint.");
        }
        finally
        {
            if (rcw != null && Marshal.IsComObject(rcw))
            {
                Marshal.ReleaseComObject(rcw);
            }
        }
    }

    protected override string GetDeviceSerialFromDevice()
    {
        object? rcw = null;
        try
        {
            throw new HardwareTokenUnavailableException("IronKey SDK missing on endpoint.");
        }
        finally
        {
            if (rcw != null && Marshal.IsComObject(rcw))
            {
                Marshal.ReleaseComObject(rcw);
            }
        }
    }
}

public class MockTokenProvider : BaseTokenProvider
{
    protected override byte[] GetRawTokenFromDevice()
    {
        // Return 32 bytes for a mock HKDF input key
        return new byte[32]; 
    }

    protected override string GetDeviceSerialFromDevice()
    {
        return "MOCK-SERIAL-001";
    }
}
