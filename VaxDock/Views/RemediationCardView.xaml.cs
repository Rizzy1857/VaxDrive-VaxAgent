using System.Windows;
using VaxDrive.Models;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Views;

public partial class RemediationCardView : Window
{
    private readonly int _findingId;
    private readonly FindingsStateManager _stateManager;

    public RemediationCardView(int findingId, RemediationCard card)
    {
        InitializeComponent();
        _findingId = findingId;
        _stateManager = new FindingsStateManager();

        RiskTitle.Text = card.Title;
        StepsList.ItemsSource = card.Steps;
    }

    private void Escalate_Click(object sender, RoutedEventArgs e)
    {
        _stateManager.Escalate(_findingId);
        MessageBox.Show("Finding escalated.", "Status Updated", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void Resolve_Click(object sender, RoutedEventArgs e)
    {
        _stateManager.MarkResolved(_findingId);
        MessageBox.Show("Finding marked as resolved.", "Status Updated", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
