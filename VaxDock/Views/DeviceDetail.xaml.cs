using System.Windows;
using System.Windows.Input;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Views;

public partial class DeviceDetail : Window
{
    private readonly string _deviceId;

    public DeviceDetail(string deviceId)
    {
        InitializeComponent();
        _deviceId = deviceId;
        Loaded += DeviceDetail_Loaded;
    }

    private void DeviceDetail_Loaded(object sender, RoutedEventArgs e)
    {
        DeviceIdText.Text = _deviceId;
        RefreshFindings();
        var history = App.ScanRepo.GetScanHistory(_deviceId);
        if (history.Count > 0)
        {
            var latest = history.Last();
            CompletenessText.Text = latest.Completeness;
            DefinitionsAgeText.Text = string.IsNullOrEmpty(latest.DefinitionsPackGenerated) ? "Unknown" : latest.DefinitionsPackGenerated;
            LastScanText.Text = latest.Timestamp;

            // Determine status
            bool isStale = DateTime.TryParse(latest.Timestamp, out DateTime scanTime) && (DateTime.UtcNow - scanTime).TotalDays > 30;
            bool isIncomplete = latest.Completeness != "100%";
            bool isHighRisk = latest.RiskScore > 75;

            if (isStale)
            {
                AssessmentStatusText.Text = "STALE";
                AssessmentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
            else if (isIncomplete || isHighRisk)
            {
                AssessmentStatusText.Text = "WARNING";
                AssessmentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)); // Orange
            }
            else
            {
                AssessmentStatusText.Text = "HEALTHY";
                AssessmentStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 255, 107)); // Green
            }
        }

        var summaries = App.DeviceRepo.GetDeviceSummaries();
        var currentDevice = System.Linq.Enumerable.FirstOrDefault(summaries, d => d.Id == _deviceId);
        if (currentDevice != null)
        {
            foreach (System.Windows.Controls.ComboBoxItem item in CriticalityCombo.Items)
            {
                if (item.Content.ToString() == currentDevice.AssetCriticality)
                {
                    CriticalityCombo.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void Criticality_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CriticalityCombo.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem && selectedItem.Content != null)
        {
            App.DeviceRepo.UpdateAssetCriticality(_deviceId, selectedItem.Content?.ToString() ?? "UNCLASSIFIED");
        }
    }

    private void Suppress_Click(object sender, RoutedEventArgs e)
    {
        if (FindingsGrid.SelectedItem is FindingDto selectedFinding)
        {
            // Defaulting to 30 days mandatory expiry for MVP
            App.FindingRepo.SuppressFinding(selectedFinding.Id, "Operator Suppressed", 30);
            RefreshFindings();
            MessageBox.Show("Finding suppressed for 30 days.", "Suppressed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RefreshFindings()
    {
        var findings = App.FindingRepo.GetByDevice(_deviceId);
        FindingsGrid.ItemsSource = findings;
    }

    private void FindingsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindingsGrid.SelectedItem is FindingDto selectedFinding && !string.IsNullOrEmpty(selectedFinding.RemediationId))
        {
            // Placeholder: Typically you'd fetch the specific card from DefinitionLoader
            // For now, stubbing the data to demonstrate the non-technical format requirement.
            var stubCard = new VaxDrive.Models.RemediationCard {
                Id = selectedFinding.RemediationId,
                Title = "What is the risk: " + selectedFinding.CveId,
                Status = "VendorAdvisory"
            };
            
            var cardView = new RemediationCardView(selectedFinding.Id, stubCard);
            cardView.Owner = this;
            cardView.ShowDialog();
            
            // Refresh DB states
            RefreshFindings();
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"VaxDrive_{_deviceId}_Findings.csv"
        };
        if (sfd.ShowDialog() == true)
        {
            var exporter = new VaxDrive.VaxDock.Services.ExportService();
            exporter.ExportDeviceCsv(_deviceId, sfd.FileName);
            MessageBox.Show($"Exported CSV to {sfd.FileName}", "Export Complete");
        }
    }

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var exporter = new VaxDrive.VaxDock.Services.ExportService();
        // Uses PrintDialog to "print to PDF" per the roadmap
        exporter.ExportDevicePdf(_deviceId, string.Empty);
    }
}
