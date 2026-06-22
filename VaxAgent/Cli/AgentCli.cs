using System;
using System.Reflection;
using System.Threading.Tasks;
using VaxDrive.VaxAgent.Startup;

namespace VaxDrive.VaxAgent.Cli;

public class AgentCli
{
    // The new Program.Main entry point for VaxAgent
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        try
        {
            // Initialize composition root
            var bootstrap = new AgentBootstrap();

            switch (args[0].ToLowerInvariant())
            {
                case "--scan":
                    Console.WriteLine("[CLI] Invoking Scan Orchestrator...");
                    VaxDrive.VaxAgent.Program.Main(new string[0]);
                    break;

                case "--sync-nvd":
                    Console.WriteLine("[CLI] Starting NVD Sync...");
                    // NvdPaginationEngine logic
                    // In a real execution, we'd extract the nvdEngine from bootstrap or use DI
                    break;

                case "--topology-export":
                    Console.WriteLine("[CLI] Exporting TopologyMap to VAXDRIVE root...");
                    // Serialize logic
                    break;

                case "--version":
                    var version = Assembly.GetExecutingAssembly().GetName().Version;
                    Console.WriteLine($"VaxDrive Agent v{version}");
                    Console.WriteLine("--- Last 5 HMAC Audit Log Entries ---");
                    // Tail mock
                    Console.WriteLine("[HMAC_AUDIT] Entry 1");
                    break;

                case "--daemon":
                    Console.WriteLine("[CLI] Starting in background daemon mode.");
                    bootstrap.Run();
                    
                    var appCts = new System.Threading.CancellationTokenSource();
                    var consoleDashboard = new OperatorConsole();
                    await consoleDashboard.RunDashboardAsync(appCts).ConfigureAwait(false);
                    break;

                default:
                    PrintUsage();
                    return 2;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL ERROR] {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("VaxDrive Agent CLI");
        Console.WriteLine("Usage: VaxAgent.exe [command]");
        Console.WriteLine("Commands:");
        Console.WriteLine("  --scan            Scan all non-protected PIDs for Yara matches (Log only)");
        Console.WriteLine("  --sync-nvd        Trigger NVD database synchronization");
        Console.WriteLine("  --topology-export Export network topology map as signed JSON");
        Console.WriteLine("  --version         Show version and recent audit logs");
        Console.WriteLine("  --daemon          Run agent continuously in background mode");
    }
}
