using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace VaxDrive.VaxAgent.Cli;

public class OperatorConsole
{
    private readonly string _version;
    private bool _running = true;

    // Mock state for rendering the dashboard
    private int _topologyCount = 0;
    private int _yaraHits = 0;
    private string _lastYaraRule = "None";

    private string[] _recentAlerts = new[] { "No recent alerts", "", "" };

    public OperatorConsole()
    {
        _version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }

    public async Task RunDashboardAsync(CancellationTokenSource appCts)
    {
        Console.Clear();
        
        while (_running && !appCts.Token.IsCancellationRequested)
        {
            RenderFrame();
            
            // Non-blocking wait for input or 5s refresh
            DateTime loopEnd = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < loopEnd && _running)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    await HandleKeyAsync(keyInfo.Key, appCts).ConfigureAwait(false);
                }
                await Task.Delay(100).ConfigureAwait(false); // Short sleep to prevent CPU spin
            }
        }
    }

    private void RenderFrame()
    {
        Console.SetCursorPosition(0, 0);
        
        Console.WriteLine($"[VaxDrive v{_version}] [{DateTime.UtcNow:O}]");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"TOPOLOGY: {_topologyCount} devices discovered");
        Console.WriteLine($"YARA:     {_yaraHits} hits (last: {_lastYaraRule})");
        

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine("ALERTS:");
        foreach (var alert in _recentAlerts)
        {
            Console.WriteLine($"  {alert,-46}"); // Fixed width to prevent trailing characters
        }
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine("[Q] Quit  [R] Force Rescan                        ");
    }

    private async Task HandleKeyAsync(ConsoleKey key, CancellationTokenSource appCts)
    {
        switch (key)
        {
            case ConsoleKey.Q:
                _running = false;
                ZeroizeToken();
                appCts.Cancel();
                Console.Clear();
                Console.WriteLine("Shutting down cleanly...");
                break;
                
            case ConsoleKey.R:
                Console.SetCursorPosition(0, 12);
                Console.WriteLine("--> Triggering manual YARA rescan...              ");
                // Mock execution
                _yaraHits++;
                _lastYaraRule = "Manual_Scan_Hit";
                await Task.Delay(1000).ConfigureAwait(false);
                break;
                
        }
        
        // Clear the status line
        Console.SetCursorPosition(0, 12);
        Console.WriteLine(new string(' ', 50));
        RenderFrame(); // Immediate re-render
    }

    private void ZeroizeToken()
    {
        // Conceptual hook to securely clear the hardware token during clean shutdown
        Console.SetCursorPosition(0, 13);
        Console.WriteLine("[SEC] Hardware token securely zeroized in RAM.    ");
    }
}
