using Avalonia;
using Avalonia.Controls;
using VGC.Maps;
using VGC.Mission;
using VGC.ViewModels;
using VGC.Views.Controls;

namespace VGC.Views;

public partial class PlanView : UserControl
{
    public PlanView()
    {
        InitializeComponent();
        if (this.FindControl<FlightMapControl>("PlanMapControl") is { } map)
        {
            map.MapClicked += OnPlanMapClicked;
            map.ViewportChanged += OnPlanMapViewportChanged;
        }
    }

    private void OnPlanViewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var rightPanel = this.FindControl<Border>("PlanRightPanel");
        var toggle = this.FindControl<Button>("PlanPanelToggle");
        var summary = this.FindControl<Border>("PlanSummaryPanel");
        var hint = this.FindControl<Border>("PlanClickHint");
        var toolStrip = this.FindControl<Border>("PlanToolStrip");
        var height = e.NewSize.Height;
        if (toolStrip is not null)
        {
            toolStrip.Width = ScreenLayout.Clamp(width * 0.055, 70, 92);
        }

        if (rightPanel is not null)
        {
            rightPanel.Width = ScreenLayout.RightPanelWidth(width);
            rightPanel.MaxWidth = ScreenLayout.Clamp(width * 0.34, 320, 460);
        }

        if (toggle is not null && rightPanel is not null)
        {
            toggle.Margin = new Thickness(0, 0, rightPanel.Width + 18, 0);
        }

        if (toolStrip is not null)
        {
            toolStrip.MinWidth = ScreenLayout.Clamp(width * 0.055, 70, 92);
        }

        if (summary is not null)
        {
            summary.MaxWidth = ScreenLayout.Clamp(width * 0.33, 320, 480);
        }

        if (hint is not null)
        {
            hint.MaxWidth = ScreenLayout.Clamp(width * 0.28, 280, 420);
        }
    }

    private static Rect BuildCenterViewport(double width, double height, Border toolStrip, Border rightPanel, Border? summary, Border? hint)
    {
        var leftToolWidth = toolStrip.Width + ScreenMetrics.ToolsMargin;
        var rightToolWidth = (rightPanel.IsVisible ? rightPanel.Width : 0) + ScreenMetrics.ToolsMargin;
        var topInset = (hint?.Bounds.Height ?? 36) + ScreenMetrics.ToolsMargin * 2;
        var bottomInset = (summary?.Bounds.Height ?? 96) + ScreenMetrics.ToolsMargin * 2;
        var viewportWidth = Math.Max(120, width - leftToolWidth - rightToolWidth - ScreenMetrics.ToolsMargin * 2);
        var viewportHeight = Math.Max(120, height - topInset - bottomInset);
        return new Rect(leftToolWidth + ScreenMetrics.ToolsMargin, topInset, viewportWidth, viewportHeight);
    }

    private void OnPlanMapClicked(object? sender, MapGeoPoint point)
    {
        if (DataContext is PlanViewModel viewModel)
        {
            viewModel.ApplyMapClick(new PlanCoordinate(point.Latitude, point.Longitude, point.Altitude));
        }
    }

    private void OnPlanMapViewportChanged(object? sender, MapViewportChangedEventArgs e)
    {
        if (DataContext is PlanViewModel viewModel)
        {
            viewModel.MarkMapManuallyMoved(new MapViewport(new MapCoordinate(e.Center.Latitude, e.Center.Longitude, e.Center.Altitude), e.ZoomLevel));
        }
    }
}
