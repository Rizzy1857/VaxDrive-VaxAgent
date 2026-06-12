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
                Steps = new System.Collections.Generic.List<string> { 
                    "1. Disconnect the device from the network immediately.", 
                    "2. Contact the Plant Security Lead.", 
                    "3. Await approval before installing patches." 
                }
            };
            
            var cardView = new RemediationCardView(selectedFinding.Id, stubCard);
            cardView.Owner = this;
            cardView.ShowDialog();
            
            // Refresh DB states
            RefreshFindings();
        }
    }
}
