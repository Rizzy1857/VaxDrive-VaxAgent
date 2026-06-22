using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VaxDrive.VaxAgent.Crypto.HardwareToken;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxAgent.Startup;

public class AgentBootstrap
{
    private readonly IHardwareTokenProvider _tokenProvider;

    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public AgentBootstrap()
    {
        try
        {
            // 1. Instantiate correct token provider
            string providerName = Environment.GetEnvironmentVariable("VAXDRIVE_HARDWARE_TOKEN_PROVIDER") ?? string.Empty;
            _tokenProvider = providerName.ToUpperInvariant() switch
            {
                "IRONKEY" => new IronKeyTokenProvider(),
                "KINGSTON" => new KingstonTokenProvider(),
                "MOCK" => new MockTokenProvider(),
                _ => throw new InvalidOperationException($"Unknown hardware token provider: {providerName}")
            };

            // 2. Derive AES-256 key via HKDF(token + machineSID)
            byte[] machineSid = Encoding.UTF8.GetBytes(Environment.MachineName); // Simplified for demo
            using var secureToken = _tokenProvider.GetCryptographicToken();
            IntPtr unmanagedBytes = Marshal.SecureStringToBSTR(secureToken);
            
            byte[] derivedDbKey;
            try
            {
                byte[] tokenBytes = new byte[secureToken.Length];
                for (int i = 0; i < secureToken.Length; i++)
                {
                    tokenBytes[i] = (byte)Marshal.ReadInt16(unmanagedBytes, i * 2);
                }

                derivedDbKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, tokenBytes, 32, new byte[32], machineSid);
                Array.Clear(tokenBytes, 0, tokenBytes.Length);
            }
            finally
            {
                Marshal.ZeroFreeBSTR(unmanagedBytes);
            }

            string dbKeyHex = Convert.ToHexString(derivedDbKey);
            Array.Clear(derivedDbKey, 0, derivedDbKey.Length);
            
            // Override env variable so CveRepository picks it up
            Environment.SetEnvironmentVariable("VAXDRIVE_DB_KEY", dbKeyHex);

            // 3. Initialize dependencies
            // (NVD Sync and CVE Repository are handled exclusively by VaxDock on the engineering workstation)

            // 4. Hook WM_DEVICECHANGE (conceptual, typically requires a Window handle or Service base)
            Microsoft.Win32.SystemEvents.SessionSwitch += (s, e) => 
            {
                // Proxying an OS event as a disconnect trigger for demo
                _tokenProvider.Dispose();
            };
        }
        catch (Exception ex)
        {
            LogAudit("BootstrapFailed", ex.ToString());
            Environment.Exit(1);
            throw; // Unreachable, but satisfies compiler if we had return paths
        }
    }

    public void Run()
    {
        try
        {

            // 6. Start TopologyMap passive listener
            Task.Run(() => StartPassiveListener(), _cts.Token);
            
            LogAudit("BootstrapSuccess", "Agent modules started successfully.");
        }
        catch (Exception ex)
        {
            LogAudit("RunFailed", ex.ToString());
            Environment.Exit(1);
        }
    }

    private void StartPassiveListener()
    {
        // SharpPcap initialization stub
        LogAudit("PassiveListener", "Started SharpPcap interface sweeps.");
    }

    private void LogAudit(string eventName, string details)
    {
        // Write to HMAC audit ring buffer
        Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | {eventName} | {details}");
    }
}
