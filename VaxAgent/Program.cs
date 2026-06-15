using System;
using System.IO;
using VaxDrive.Models;
using VaxDrive.VaxAgent.Checks;
using VaxDrive.VaxAgent.Crypto;
using VaxDrive.VaxAgent.Definitions;
using VaxDrive.VaxAgent.Output;

namespace VaxDrive.VaxAgent;

public static class Program
{
    public static void Main(string[] args)
    {
        VaxDrive.VaxAgent.Diagnostics.CrashLogger.Initialize();
        try
        {
            Console.WriteLine("[*] Starting VaxAgent...");

            // 1. Resolve paths
            string drivePath = GetDrivePath(args);
            string defsPath = Path.Combine(drivePath, "boot", "definitions.json");
            string resultsPath = Path.Combine(drivePath, "results");

            if (!Directory.Exists(resultsPath))
            {
                Directory.CreateDirectory(resultsPath);
            }

            // 2. Load Definitions
            var loader = new DefinitionLoader();
            DefinitionPack? pack = loader.Load(defsPath);
            if (pack == null)
            {
                Console.WriteLine("[!] Warning: Could not load definition pack. Proceeding with baseline checks only.");
            }

            // 3. Initialize Context
            var context = new ScanContext
            {
                DefinitionsPath = defsPath,
                ResultsPath = resultsPath
            };

            // 4. Assemble Checks
            var checks = new ICheck[]
            {
                new FirmwareCheck(),
                new OsCheck(),
                new InstalledSoftwareCheck(),
                new ServicesCheck(),
                new ScheduledTasksCheck(),
                new OpenPortsCheck(),
                new UsbHistoryCheck(),
                new RogueProcessCheck(pack),
                new CveMatchCheck(pack),
                new PlcBannerCheck()
            };

            // 5. Run Orchestrator
            var orchestrator = new ScanOrchestrator(checks);
            ScanResult result = orchestrator.RunAll(context);

            // 6. Crypto & Output
            byte[] hardwareToken = HardwareTokenProvider.GetTokenBytes(drivePath);
            byte[] aesKey = KeyDerivation.DeriveAesKey(hardwareToken);
            byte[] hmacKey = KeyDerivation.DeriveHmacKey(hardwareToken);

            var writer = new VaxFileWriter();
            writer.Write(result, resultsPath, aesKey, hmacKey);

            Console.WriteLine($"[+] Scan complete. Payload encrypted and signed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] CRITICAL FAILURE: {ex.Message}");
            // Return non-zero exit code so any calling script knows it failed
            Environment.ExitCode = 1;
        }
    }
    // Main entry point for the VaxAgent console application. Ties all phases together.
    // Returns void (sets Environment.ExitCode on failure).

    internal static string GetDrivePath(string[] args)
    {
        if (args.Length > 0 && Directory.Exists(args[0]))
        {
            return args[0];
        }

        string path = null;
        string source = null;

        try
        {
            string modulePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(modulePath))
            {
                path = Path.GetDirectoryName(modulePath);
                source = "Process.MainModule";
            }
        }
        catch { }

        if (string.IsNullOrEmpty(path) || path.IndexOf("System32", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            try
            {
                string asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(asmPath))
                {
                    path = Path.GetDirectoryName(asmPath);
                    source = "Assembly.Location";
                }
            }
            catch { }
        }

        if (string.IsNullOrEmpty(path) || path.IndexOf("System32", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            path = AppDomain.CurrentDomain.BaseDirectory;
            source = "AppDomain.BaseDirectory";
            
            if (!string.IsNullOrEmpty(path) && (path.EndsWith("\\") || path.EndsWith("/")))
            {
                path = path.Substring(0, path.Length - 1);
            }
        }

        if (path != null && path.IndexOf("System32", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            throw new InvalidOperationException("Fatal: Drive path resolved to System32. Execution context is invalid.");
        }

        Console.WriteLine($"[HMAC_AUDIT] {DateTime.UtcNow:O} | Program | Drive path resolved via {source}");
        return path ?? AppDomain.CurrentDomain.BaseDirectory;
    }
    // Determines the root path of the VaxDrive from arguments or execution context.
    // Returns a string representing the drive root path.
}
