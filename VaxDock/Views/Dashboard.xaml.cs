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
        // Stand-in export destination; normally invokes SaveFileDialog
        string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "FindingsExport.csv");
        var exporter = new VaxDrive.VaxDock.Services.ExportService();
        exporter.ExportFindingsCsv(path);
        MessageBox.Show($"Exported to {path}", "Export Complete");
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
