using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using VaxDrive.Models;

namespace VaxDrive.VaxDock.Views;

public partial class SyncedDefinitions : Window
{
    public SyncedDefinitions()
    {
        InitializeComponent();
        Loaded += SyncedDefinitions_Loaded;
    }

    private void SyncedDefinitions_Loaded(object sender, RoutedEventArgs e)
    {
        string localCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VaxDock", "definitions");
        
        if (!Directory.Exists(localCacheDir))
        {
            MessageBox.Show("No definitions folder found. Have you synced NVD yet?", "No Definitions", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            return;
        }

        var latestPackFile = new DirectoryInfo(localCacheDir)
                            .GetFiles("*.json")
                            .OrderByDescending(f => f.LastWriteTime)
                            .FirstOrDefault();

        if (latestPackFile == null)
        {
            MessageBox.Show("No definition packs found in cache. Have you synced NVD yet?", "No Definitions", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            return;
        }

        try
        {
            string jsonContent = File.ReadAllText(latestPackFile.FullName);
            var pack = JsonSerializer.Deserialize<DefinitionPack>(jsonContent);

            if (pack != null)
            {
                PackVersionText.Text = pack.PackVersion;
                GeneratedText.Text = pack.Generated;
                RulesCountText.Text = pack.SoftwareCveRules.Count.ToString();
                RulesList.ItemsSource = pack.SoftwareCveRules;
            }
        }
        catch (JsonException ex)
        {
            MessageBox.Show($"Failed to load definitions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Failed to load definitions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void RulesList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var sv = sender as ScrollViewer;
        if (sv == null) return;

        // Calculate opacity for Top Gradient (fades in as you scroll down)
        TopGradient.Opacity = Math.Min(sv.VerticalOffset / 50.0, 1.0);

        // Calculate opacity for Bottom Gradient (fades out as you reach the bottom)
        double bottomDistance = sv.ScrollableHeight - sv.VerticalOffset;
        BottomGradient.Opacity = sv.ScrollableHeight <= 0 ? 0 : Math.Min(bottomDistance / 50.0, 1.0);
    }
}
