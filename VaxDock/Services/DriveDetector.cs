using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VaxDrive.VaxDock.Services;

public sealed class DriveDetector : IDisposable
{
    private readonly IngestPipeline _pipeline;
    private readonly CancellationTokenSource _cts;
    
    // For dev: deterministic keys mimicking the stub in Phase 2
    private readonly byte[] _aesKey = new byte[32];
    private readonly byte[] _hmacKey = new byte[32];

    public event Action? OnIngestCompleted;

    public DriveDetector(IngestPipeline pipeline)
    {
        _pipeline = pipeline;
        _cts = new CancellationTokenSource();
    }

    public void Start()
    {
        // TRAP AVOIDED: Background polling thread. Does not block UI.
        Task.Run(PollDrivesAsync, _cts.Token);
    }

    private async Task PollDrivesAsync()
    {
        string? lastDetectedDrive = null;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                DriveInfo? vaxDrive = drives.FirstOrDefault(d => 
                    d.IsReady && d.VolumeLabel.Equals("VAXDRIVE", StringComparison.OrdinalIgnoreCase));

                if (vaxDrive != null)
                {
                    string drivePath = vaxDrive.RootDirectory.FullName;
                    if (drivePath != lastDetectedDrive)
                    {
                        lastDetectedDrive = drivePath;
                        await _pipeline.RunAsync(drivePath, _hmacKey, _aesKey);
                        OnIngestCompleted?.Invoke();
                    }
                }
                else
                {
                    lastDetectedDrive = null;
                }
            }
            catch (IOException)
            {
                // Ignored: drive yanked or WMI delay mid-poll. Poller will retry in 2s.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignored: permissions issue
            }

            await Task.Delay(TimeSpan.FromSeconds(2), _cts.Token);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
