using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VGC.ViewModels;
using VGC.Views.Controls;

namespace VGC.Views;

public partial class PlanView : UserControl
{
    public PlanView()
    {
        InitializeComponent();
    }

    private void OnPlanViewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var rightPanel = this.FindControl<Border>("PlanRightPanel");
        var toggle = this.FindControl<Button>("PlanPanelToggle");
        var summary = this.FindControl<Border>("PlanSummaryPanel");
        var hint = this.FindControl<Border>("PlanClickHint");
        var toolStrip = this.FindControl<Border>("PlanToolStrip");
        var centerLayer = this.FindControl<StackPanel>("PlanMapCenterLayer");
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

        if (centerLayer is not null && toolStrip is not null && rightPanel is not null)
        {
            var viewport = BuildCenterViewport(width, height, toolStrip, rightPanel, summary, hint);
            var desiredWidth = Math.Min(Math.Max(centerLayer.Bounds.Width, centerLayer.DesiredSize.Width), viewport.Width);
            var desiredHeight = Math.Min(Math.Max(centerLayer.Bounds.Height, centerLayer.DesiredSize.Height), viewport.Height);
            centerLayer.Margin = new Thickness(
                viewport.X + Math.Max(0, (viewport.Width - desiredWidth) / 2),
                viewport.Y + Math.Max(0, (viewport.Height - desiredHeight) / 2),
                0,
                0);
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

    private void OnPlanMapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control surface || DataContext is not PlanViewModel viewModel)
        {
            return;
        }

        var point = e.GetPosition(surface);
        if (surface.Bounds.Width <= 0 || surface.Bounds.Height <= 0)
        {
            return;
        }

        viewModel.ApplyMapClick(point.X / surface.Bounds.Width, point.Y / surface.Bounds.Height);
        e.Handled = true;
    }
}
