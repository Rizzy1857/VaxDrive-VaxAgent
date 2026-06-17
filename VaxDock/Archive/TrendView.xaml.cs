using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VaxDrive.VaxDock.Data;

namespace VaxDrive.VaxDock.Views;

public partial class TrendView : UserControl
{
    private IReadOnlyList<ScanSummary>? _scans;

    public TrendView()
    {
        InitializeComponent();
    }

    public void LoadHistory(IReadOnlyList<ScanSummary> scans)
    {
        _scans = scans;
        DrawChart();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawChart();
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        if (_scans == null || _scans.Count == 0)
        {
            NoDataText.Visibility = Visibility.Visible;
            return;
        }

        NoDataText.Visibility = Visibility.Collapsed;

        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        if (_scans.Count == 1)
        {
            double cx = width / 2;
            double cy = height / 2;
            DrawPoint(cx, cy, _scans[0].ResolvedCount > 0);
            return;
        }

        int maxCritical = _scans.Max(s => s.CriticalCount);
        if (maxCritical == 0) maxCritical = 1;

        double scaleX = width / (_scans.Count - 1);
        double scaleY = (height * 0.8) / maxCritical;

        var points = new PointCollection();
        var pointData = new List<(double x, double y, bool hasResolved)>();

        for (int i = 0; i < _scans.Count; i++)
        {
            var s = _scans[i];
            double x = i * scaleX;
            double y = height - (height * 0.1) - (s.CriticalCount * scaleY);
            
            points.Add(new Point(x, y));
            pointData.Add((x, y, s.ResolvedCount > 0));
        }

        var polyline = new Polyline
        {
            Points = points,
            Stroke = Brushes.OrangeRed,
            StrokeThickness = 2
        };

        ChartCanvas.Children.Add(polyline);

        foreach (var p in pointData)
        {
            DrawPoint(p.x, p.y, p.hasResolved);
        }
    }

    private void DrawPoint(double x, double y, bool hasResolved)
    {
        var ellipse = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = hasResolved ? Brushes.LimeGreen : Brushes.OrangeRed,
            ToolTip = hasResolved ? "Resolved Findings" : "Findings Present"
        };
        Canvas.SetLeft(ellipse, x - 4);
        Canvas.SetTop(ellipse, y - 4);
        ChartCanvas.Children.Add(ellipse);
    }
}
