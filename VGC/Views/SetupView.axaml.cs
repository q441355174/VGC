using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VGC.Setup;
using VGC.ViewModels;
using VGC.Views.Controls;

namespace VGC.Views;

public partial class SetupView : UserControl
{
    private SetupViewModel? _subscribedVm;

    public SetupView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_subscribedVm is null)
        {
            return;
        }

        _subscribedVm.PropertyChanged -= OnSetupViewModelPropertyChanged;
        _subscribedVm = null;
    }

    private void OnSetupViewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var sidebar = this.FindControl<Border>("SetupSidebar");
        var detail = this.FindControl<Border>("SetupDetailPanel");
        if (sidebar is not null)
        {
            sidebar.Width = ScreenLayout.Clamp(width * 0.28, 240, 340);
        }

        if (detail is not null)
        {
            detail.MinWidth = ScreenLayout.Clamp(width * 0.46, 420, 720);
            detail.MaxWidth = ScreenLayout.Clamp(width * 0.72, 640, 980);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnSetupViewModelPropertyChanged;
            _subscribedVm = null;
        }

        if (DataContext is SetupViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.PropertyChanged += OnSetupViewModelPropertyChanged;
            UpdateDetailPanelVisibility(vm.DetailTabKind);
        }
    }

    private void OnSetupViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (sender is not SetupViewModel vm)
        {
            return;
        }

        if (args.PropertyName == nameof(SetupViewModel.SelectedDetailTab) || args.PropertyName == nameof(SetupViewModel.DetailTabKind))
        {
            UpdateDetailPanelVisibility(vm.DetailTabKind);
        }
    }

    private void OnShowSetupSummary(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
        {
            vm.ShowSummary();
        }
    }

    private void OnShowSetupParameters(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
        {
            vm.ShowParameters();
        }
    }

    private void OnComponentButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string componentId && DataContext is SetupViewModel vm)
        {
            vm.SelectComponent(componentId);
        }
    }

    private void OnCalibrateCompass(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
            vm.StartSensorCalibration(SensorCalibrationType.Compass);
    }

    private void OnCalibrateAccel(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
            vm.StartSensorCalibration(SensorCalibrationType.Accelerometer);
    }

    private void OnCalibrateGyro(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
            vm.StartSensorCalibration(SensorCalibrationType.Gyroscope);
    }

    private void OnCalibrateLevel(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
            vm.StartSensorCalibration(SensorCalibrationType.Level);
    }

    private void OnConfirmCalibration(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
            vm.ConfirmSensorCalibration();
    }

    private void OnCancelCalibration(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm)
            vm.CancelSensorCalibration();
    }

    private void OnSaveSafetyChanges(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel vm) return;

        var safetyPanel = this.FindControl<StackPanel>("SafetyDetailPanel");
        if (safetyPanel is null) return;

        CommitFactControlsRecursive(safetyPanel, vm);
    }

    private static void CommitFactControlsRecursive(Control parent, SetupViewModel vm)
    {
        if (parent is FactTextField textField && !string.IsNullOrEmpty(textField.ParameterName))
        {
            if (textField.Validate())
            {
                vm.CommitSafetyParameterEdit(textField.ParameterName, textField.Text);
            }
        }
        else if (parent is FactComboBox comboBox && !string.IsNullOrEmpty(comboBox.ParameterName))
        {
            comboBox.CommitSelection();
            var selected = comboBox.SelectedItem;
            if (selected is not null)
            {
                vm.CommitSafetyParameterEdit(comboBox.ParameterName, selected.Value.ToString());
            }
        }

        if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control control)
                    CommitFactControlsRecursive(control, vm);
            }
        }
        else if (parent is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            CommitFactControlsRecursive(decoratorChild, vm);
        }
        else if (parent is ContentControl contentControl && contentControl.Content is Control contentChild)
        {
            CommitFactControlsRecursive(contentChild, vm);
        }
        else if (parent is ItemsControl itemsControl)
        {
            for (var i = 0; i < itemsControl.ItemCount; i++)
            {
                var container = itemsControl.ContainerFromIndex(i);
                if (container is Control containerControl)
                    CommitFactControlsRecursive(containerControl, vm);
            }
        }
    }

    private void UpdateDetailPanelVisibility(SetupDetailTabKind tab)
    {
        var safetyPanel = this.FindControl<StackPanel>("SafetyDetailPanel");
        var sensorsPanel = this.FindControl<StackPanel>("SensorsDetailPanel");
        var radioPanel = this.FindControl<StackPanel>("RadioDetailPanel");
        var flightModesPanel = this.FindControl<StackPanel>("FlightModesDetailPanel");
        var genericPanel = this.FindControl<StackPanel>("GenericDetailPanel");

        if (safetyPanel is not null) safetyPanel.IsVisible = tab == SetupDetailTabKind.Safety;
        if (sensorsPanel is not null) sensorsPanel.IsVisible = tab == SetupDetailTabKind.Sensors;
        if (radioPanel is not null) radioPanel.IsVisible = tab == SetupDetailTabKind.Radio;
        if (flightModesPanel is not null) flightModesPanel.IsVisible = tab == SetupDetailTabKind.FlightModes;

        var isSpecific = tab is SetupDetailTabKind.Safety or SetupDetailTabKind.Sensors or SetupDetailTabKind.Radio or SetupDetailTabKind.FlightModes or SetupDetailTabKind.None;
        if (genericPanel is not null)
        {
            genericPanel.IsVisible = !isSpecific && tab != SetupDetailTabKind.None;

            if (genericPanel.IsVisible && DataContext is SetupViewModel vm)
            {
                var component = vm.SelectedComponent;
                if (component is not null)
                {
                    var title = this.FindControl<TextBlock>("GenericDetailTitle");
                    var summary = this.FindControl<TextBlock>("GenericDetailSummary");
                    var status = this.FindControl<TextBlock>("GenericDetailStatus");
                    var missing = this.FindControl<TextBlock>("GenericDetailMissing");

                    if (title is not null) title.Text = component.Title;
                    if (summary is not null) summary.Text = component.Summary;
                    if (status is not null) status.Text = $"Status: {component.Readiness} - {component.StatusText}";
                    if (missing is not null) missing.Text = component.MissingParameters.Count > 0
                        ? $"Missing: {string.Join(", ", component.MissingParameters)}"
                        : "";
                }
            }
        }
    }
}
