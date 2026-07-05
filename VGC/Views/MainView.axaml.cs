using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using VGC.ViewModels;
using VGC.Views.Controls;

namespace VGC.Views;

public partial class MainView : UserControl
{
    private ShellViewModel? _subscribedVm;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnShellPropertyChanged;
            _subscribedVm = null;
        }

        if (DataContext is ShellViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.PropertyChanged += OnShellPropertyChanged;
            UpdateDynamicLayout(Bounds.Width);
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_subscribedVm is null)
        {
            return;
        }

        _subscribedVm.PropertyChanged -= OnShellPropertyChanged;
        _subscribedVm = null;
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IndicatorDrawerAnchorX)
            or nameof(ShellViewModel.IsIndicatorDrawerExpanded)
            or nameof(ShellViewModel.IsIndicatorDrawerActive))
        {
            UpdateDynamicLayout(Bounds.Width);
        }
    }

    private void OnMainViewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateDynamicLayout(e.NewSize.Width);
    }

    private void UpdateDynamicLayout(double width)
    {
        var indicatorDrawer = this.FindControl<Border>("IndicatorDrawerBorder");
        var toolSelect = this.FindControl<Border>("ToolSelectPopup");

        if (indicatorDrawer is not null && DataContext is ShellViewModel vm && width > 0)
        {
            var drawerWidth = vm.IsIndicatorDrawerExpanded
                ? ScreenLayout.Clamp(width * 0.48, 520, 760)
                : ScreenLayout.Clamp(width * 0.26, 280, 420);
            var maxLeft = Math.Max(ScreenMetrics.StandardMargin, width - drawerWidth - ScreenMetrics.StandardMargin);
            var left = ScreenLayout.Clamp(vm.IndicatorDrawerAnchorX - drawerWidth / 2, ScreenMetrics.StandardMargin, maxLeft);
            indicatorDrawer.Width = drawerWidth;
            indicatorDrawer.Margin = new Thickness(left, ScreenMetrics.ToolbarHeight + ScreenMetrics.StandardMargin, 0, 0);
        }

        if (toolSelect is not null && width > 0)
        {
            toolSelect.Margin = ScreenLayout.ToolSelectMargin();
            toolSelect.Width = ScreenLayout.Clamp(width * 0.16, 140, 220);
        }
    }
}
