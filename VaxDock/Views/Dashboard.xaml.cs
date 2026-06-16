using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Views;

public partial class Dashboard : Window
{
    public Dashboard()
    {
        InitializeComponent();
        Loaded += Dashboard_Loaded;
        App.Detector.OnIngestCompleted += OnIngestCompleted;
    }

    private void Dashboard_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = Services.SettingsManager.Load();
        if (!string.IsNullOrEmpty(settings.NvdApiKey))
        {
            NvdApiKeyBox.Password = settings.NvdApiKey;
        }

        RefreshData();
    }

    private void OnIngestCompleted()
    {
        // TRAP AVOIDED: Dispatcher.InvokeAsync used to update UI from background thread
        Dispatcher.InvokeAsync(RefreshData);
    }

    private void RefreshData()
    {
        var summaries = App.DeviceRepo.GetDeviceSummaries();
        DevicesGrid.ItemsSource = summaries;
        TotalDevicesText.Text = summaries.Count.ToString();

        // Cadence alerts logic refactored to SQL
        int overdueCount = App.DeviceRepo.GetOverdueDevicesCount(7);
        if (overdueCount > 0)
        {
            CadenceAlertText.Text = $"⚠️ {overdueCount} devices have not been scanned in over 7 days.";
            CadenceAlertText.Visibility = Visibility.Visible;
        }
        else
        {
            CadenceAlertText.Visibility = Visibility.Collapsed;
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "FindingsExport.csv");
        var exporter = new VaxDrive.VaxDock.Services.ExportService();
        exporter.ExportFindingsCsv(path);
        MessageBox.Show($"Exported to {path}", "Export Complete");
    }

    private async void SyncNvd_Click(object sender, RoutedEventArgs e)
    {
        string apiKey = NvdApiKeyBox.Password.Trim();
        var settings = Services.SettingsManager.Load();
        settings.NvdApiKey = apiKey;
        Services.SettingsManager.Save(settings);

        try
        {
            var syncService = new Services.NvdSyncService();
            await syncService.SyncDefinitionsAsync(apiKey);
            MessageBox.Show("NVD Definitions successfully synced and signed.", "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to sync NVD: {ex.Message}", "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewDefinitions_Click(object sender, RoutedEventArgs e)
    {
        var syncedWindow = new SyncedDefinitions();
        syncedWindow.Owner = this;
        syncedWindow.ShowDialog();
    }

    private void PrepareDrive_Click(object sender, RoutedEventArgs e)
    {
        // Detect VAXDRIVE USB
        var driveInfo = System.IO.DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.VolumeLabel == "VAXDRIVE");
        if (driveInfo == null)
        {
            MessageBox.Show("No USB drive labeled 'VAXDRIVE' was found.", "Drive Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string localCacheDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VaxDock", "definitions");
        var latestPack = new System.IO.DirectoryInfo(localCacheDir)
                            .GetFiles("*.json")
                            .OrderByDescending(f => f.LastWriteTime)
                            .FirstOrDefault();

        if (latestPack == null)
        {
            MessageBox.Show("No definition packs found in local cache. Please sync NVD first.", "Cache Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string targetDir = System.IO.Path.Combine(driveInfo.RootDirectory.FullName, "definitions");
        if (!System.IO.Directory.Exists(targetDir))
        {
            System.IO.Directory.CreateDirectory(targetDir);
        }

        string targetFile = System.IO.Path.Combine(targetDir, "definitions.json");
        System.IO.File.Copy(latestPack.FullName, targetFile, overwrite: true);

        MessageBox.Show($"Successfully prepared drive!\nCopied: {latestPack.Name}", "Prepare Drive Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (DevicesGrid == null || App.DeviceRepo == null) return;

        string deviceFilter = FilterDeviceBox.Text.ToLowerInvariant();
        string severityFilter = (FilterSeverityCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "All";

        var allSummaries = App.DeviceRepo.GetDeviceSummaries();
        
        var filtered = allSummaries.Where(s => 
        {
            if (!string.IsNullOrEmpty(deviceFilter) && !s.Id.ToLowerInvariant().Contains(deviceFilter))
                return false;

            if (severityFilter == "Critical" && s.CriticalCount == 0) return false;
            if (severityFilter == "High" && s.HighCount == 0 && s.CriticalCount == 0) return false;

            return true;
        }).ToList();

        DevicesGrid.ItemsSource = filtered;
        TotalDevicesText.Text = filtered.Count.ToString();
    }

    private void DevicesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DevicesGrid.SelectedItem is DeviceSummary selectedDevice)
        {
            var detailWindow = new DeviceDetail(selectedDevice.Id);
            detailWindow.Owner = this;
            detailWindow.ShowDialog();
        }
    }
}
