using System;
using System.IO;
using System.Windows;
using VaxDrive.VaxDock.Data;
using VaxDrive.VaxDock.Services;

namespace VaxDrive.VaxDock;

public partial class App : Application
{
    public static ScanRepository ScanRepo { get; private set; } = null!;
    public static DeviceRepository DeviceRepo { get; private set; } = null!;
    public static FindingRepository FindingRepo { get; private set; } = null!;
    public static QuarantineService Quarantine { get; private set; } = null!;
    public static IngestPipeline Pipeline { get; private set; } = null!;
    public static DriveDetector Detector { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VaxDrive.db");
        DatabaseBootstrap.Initialize(dbPath);

        Quarantine = new QuarantineService();
        ScanRepo = new ScanRepository();
        DeviceRepo = new DeviceRepository();
        FindingRepo = new FindingRepository();
        
        Pipeline = new IngestPipeline(ScanRepo, Quarantine);
        Detector = new DriveDetector(Pipeline);

        Detector.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Detector?.Dispose();
        DatabaseBootstrap.GetConnection()?.Dispose();
        base.OnExit(e);
    }
}
