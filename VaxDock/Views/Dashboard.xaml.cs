using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Views;

public partial class Dashboard : Window
{
    private bool _isCadenceAlertDismissed = false;

    public Dashboard()
    {
        InitializeComponent();
        Loaded += Dashboard_Loaded;
        App.Detector.OnIngestCompleted += OnIngestCompleted;
    }

    private void Dashboard_Loaded(object sender, RoutedEventArgs e)
    {
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
        DevicesAtRiskText.Text = CalculateDevicesAtRisk(summaries).ToString();

        if (_isCadenceAlertDismissed)
        {
            CadenceAlertBanner.Visibility = Visibility.Collapsed;
        }
        else
        {
            var appSettings = Services.SettingsManager.Load();
            int threshold = appSettings.CadenceThresholdDays;
            
            var overdueDevices = App.DeviceRepo.GetOverdueDevices(threshold);
            if (overdueDevices.Count > 0)
            {
                CadenceAlertTitle.Text = $"⚠️ {overdueDevices.Count} devices have not been scanned in over {threshold} days.";
                CadenceAlertDevices.Text = string.Join(" · ", overdueDevices.Take(3));
                if (overdueDevices.Count > 3)
                {
                    CadenceAlertDevices.Text += $" and {overdueDevices.Count - 3} more...";
                }
                CadenceAlertBanner.Visibility = Visibility.Visible;
            }
            else
            {
                CadenceAlertBanner.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void DismissCadenceAlert_Click(object sender, RoutedEventArgs e)
    {
        _isCadenceAlertDismissed = true;
        CadenceAlertBanner.Visibility = Visibility.Collapsed;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "FindingsExport.csv");
        var exporter = new VaxDrive.VaxDock.Services.ExportService();
        exporter.ExportFindingsCsv(path);
        MessageBox.Show($"Exported to {path}", "Export Complete");
    }

    private async void ActionCards_SyncClicked(object sender, EventArgs e)
    {
        string apiKey = NvdApiKeyBox.Password.Trim();

        SyncProgressBar.Visibility = Visibility.Visible;
        ActionCards.IsEnabled = false;

        var progress = new Progress<string>(msg => 
        {
            SyncStatusText.Text = msg;
        });

        try
        {
            var syncService = new Services.NvdSyncService();
            await syncService.SyncDefinitionsAsync(apiKey, progress);
            SyncStatusText.Text = "Sync Complete!";
            MessageBox.Show("NVD Definitions successfully synced and signed.", "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            SyncStatusText.Text = "Sync Failed.";
            MessageBox.Show($"Failed to sync NVD: {ex.Message}", "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
#pragma warning restore CA1031
        finally
        {
            SyncProgressBar.Visibility = Visibility.Collapsed;
            ActionCards.IsEnabled = true;
            _ = System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ => Dispatcher.InvokeAsync(() => SyncStatusText.Text = ""));
        }
    }

    private void ActionCards_ViewClicked(object sender, EventArgs e)
    {
        var syncedWindow = new SyncedDefinitions();
        syncedWindow.Owner = this;
        syncedWindow.ShowDialog();
    }

    private void ActionCards_PrepareClicked(object sender, EventArgs e)
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
        string severityFilter = "All";
        if (FilterSeverityCritical?.IsChecked == true) severityFilter = "Critical";
        else if (FilterSeverityHigh?.IsChecked == true) severityFilter = "High";
        else if (FilterSeverityMedium?.IsChecked == true) severityFilter = "Medium";
        else if (FilterSeverityLow?.IsChecked == true) severityFilter = "Low";

        var allSummaries = App.DeviceRepo.GetDeviceSummaries();
        
        var filtered = allSummaries.Where(s => 
        {
            if (!string.IsNullOrEmpty(deviceFilter) && !s.Id.ToLowerInvariant().Contains(deviceFilter))
                return false;

            if (severityFilter == "Critical" && s.CriticalCount == 0) return false;
            if (severityFilter == "High" && s.HighCount == 0 && s.CriticalCount == 0) return false;
            if (severityFilter == "Medium" && s.MediumCount == 0 && s.HighCount == 0 && s.CriticalCount == 0) return false;
            if (severityFilter == "Low" && s.LowCount == 0 && s.MediumCount == 0 && s.HighCount == 0 && s.CriticalCount == 0) return false;

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
            RefreshData(); // Refresh in case criticality changed
        }
    }

    private int CalculateDevicesAtRisk(System.Collections.Generic.IEnumerable<DeviceSummary> devices)
    {
        return devices.Count(d => d.CriticalCount > 0 || d.HighCount > 0);
    }
}
