using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VGC.ViewModels;
using VGC.Views.Controls;

namespace VGC.Views;

public partial class FlyView : UserControl
{
    public FlyView()
    {
        InitializeComponent();
        this.FindControl<MainStatusToolbarIndicator>("MainStatusIndicator")?.IndicatorClicked += (sender, _) => OpenIndicatorDrawer(IndicatorDrawerKind.MainStatus, sender as Control);
        this.FindControl<FlightModeToolbarIndicator>("FlightModeIndicator")?.IndicatorClicked += (sender, _) => OpenIndicatorDrawer(IndicatorDrawerKind.FlightMode, sender as Control);
    }

    private void OnMapAreaSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var height = e.NewSize.Height;

        var mapUnit = ScreenLayout.Clamp(Math.Min(width, height), 360, 900);
        ApplyMapOverlayLayout(mapUnit);

        var pip = this.FindControl<Border>("PipBorder");
        if (pip is not null)
        {
            var pipWidth = ScreenLayout.Clamp(width * ScreenMetrics.PipDefaultRatio, 120, width * ScreenMetrics.PipMaxRatio);
            pip.Width = pipWidth;
            pip.Height = pipWidth * ScreenMetrics.PipAspectRatio;
        }

        ApplyVirtualJoystickLayout(width, height);

        var vjPanel = this.FindControl<Grid>("VirtualJoystickPanel");
        var toolStrip = this.FindControl<Border>("FlyToolStrip");
        if (toolStrip is not null)
        {
            toolStrip.Width = ScreenLayout.Clamp(width * 0.055, 70, 92);
        }

        var attitude = this.FindControl<Control>("AttitudeHud");
        var compass = this.FindControl<Control>("CompassHud");
        var speed = this.FindControl<Control>("SpeedGauge");
        var altitude = this.FindControl<Control>("AltitudeGauge");
        var hudSize = ScreenLayout.Clamp(width * 0.14, 140, 220);
        if (attitude is not null)
        {
            attitude.Width = hudSize;
            attitude.Height = hudSize;
        }

        if (compass is not null)
        {
            compass.Width = ScreenLayout.Clamp(width * 0.11, 110, 160);
            compass.Height = compass.Width;
        }

        var gaugeHeight = ScreenLayout.Clamp(height * 0.28, 160, 240);
        if (speed is not null)
        {
            speed.Width = ScreenLayout.Clamp(width * 0.045, 48, 64);
            speed.Height = gaugeHeight;
        }

        if (altitude is not null)
        {
            altitude.Width = ScreenLayout.Clamp(width * 0.045, 48, 64);
            altitude.Height = gaugeHeight;
        }

        var payload = this.FindControl<Border>("PayloadPanel");
        if (payload is not null)
        {
            payload.Width = ScreenLayout.Clamp(width * 0.24, 280, 360);
        }

        var widgets = this.FindControl<Border>("WidgetPanel");
        if (widgets is not null)
        {
            widgets.Width = ScreenLayout.Clamp(width * 0.24, 280, 360);
        }

        var telemetry = this.FindControl<Border>("BottomTelemetryPanel");
        if (telemetry is not null)
        {
            telemetry.MaxWidth = ScreenLayout.Clamp(width * 0.42, 420, 640);
        }

        var topRight = this.FindControl<StackPanel>("FlyTopRightOverlay");
        if (topRight is not null)
        {
            topRight.MaxWidth = ScreenLayout.Clamp(width * 0.28, 280, 380);
        }

        var slider = this.FindControl<GuidedValueSlider>("AltitudeSlider");
        if (slider is not null)
        {
            slider.Width = ScreenLayout.Clamp(width * 0.065, 72, 96);
        }

        ApplyOverlaySafeArea(width, height, toolStrip, mapScale: this.FindControl<Border>("FlyMapScale"), topRight, telemetry, pip, slider, vjPanel);
    }

    private void ApplyMapOverlayLayout(double mapUnit)
    {
        SetSquare("OuterRangeRing", mapUnit * 0.52);
        SetSquare("InnerRangeRing", mapUnit * 0.26);
        SetSquare("VehicleMapMarker", ScreenLayout.Clamp(mapUnit * 0.025, 16, 24), cornerRadiusRatio: 0.5);

        var crosshairSize = ScreenLayout.Clamp(mapUnit * 0.055, 36, 52);
        var crosshairThickness = ScreenLayout.Clamp(mapUnit * 0.0025, 2, 3);
        SetSize("VehicleCrosshairVertical", crosshairThickness, crosshairSize);
        SetSize("VehicleCrosshairHorizontal", crosshairSize, crosshairThickness);

        var home = this.FindControl<Border>("HomeMapMarker");
        if (home is not null)
        {
            var homeSize = ScreenLayout.Clamp(mapUnit * 0.018, 12, 18);
            home.Width = homeSize;
            home.Height = homeSize;
            home.Margin = new Thickness(-mapUnit * 0.1, -mapUnit * 0.08, 0, 0);
        }

        var trajectory = this.FindControl<Polyline>("VehicleTrajectory");
        if (trajectory is not null)
        {
            var centerX = mapUnit * 0.5;
            var centerY = mapUnit * 0.42;
            trajectory.Points =
            [
                new Point(centerX - mapUnit * 0.07, centerY + mapUnit * 0.035),
                new Point(centerX - mapUnit * 0.03, centerY + mapUnit * 0.015),
                new Point(centerX + mapUnit * 0.025, centerY + mapUnit * 0.032)
            ];
        }
    }

    private void ApplyVirtualJoystickLayout(double width, double height)
    {
        var panelHeight = Math.Min(height * ScreenMetrics.VirtualJoystickMaxHeightRatio, ScreenMetrics.VirtualJoystickMaxPixels);
        var stickSize = ScreenLayout.Clamp(panelHeight - ScreenMetrics.LayoutMargin * 2, 96, 152);
        var thumbSize = ScreenLayout.Clamp(stickSize * 0.42, 40, 56);

        var vjPanel = this.FindControl<Grid>("VirtualJoystickPanel");
        if (vjPanel is not null)
        {
            vjPanel.MaxHeight = panelHeight;
            vjPanel.Margin = new Thickness(ScreenLayout.Clamp(width * 0.025, 16, 32), 0, ScreenLayout.Clamp(width * 0.025, 16, 32), ScreenMetrics.LayoutMargin * 3);
        }

        var vjGrid = this.FindControl<Grid>("VirtualJoystickGrid");
        if (vjGrid is not null)
        {
            vjGrid.ColumnDefinitions[1].Width = new GridLength(ScreenLayout.Clamp(width * 0.16, 140, 220));
        }

        SetStickLayout("Left", stickSize, thumbSize);
        SetStickLayout("Right", stickSize, thumbSize);
    }

    private void ApplyOverlaySafeArea(double width, double height, Border? toolStrip, Border? mapScale, StackPanel? topRight, Border? telemetry, Border? pip, GuidedValueSlider? slider, Grid? vjPanel)
    {
        var insets = BuildOverlayInsets(toolStrip, topRight, telemetry, pip, slider, vjPanel);

        if (mapScale is not null)
        {
            mapScale.Margin = new Thickness(insets.Left, insets.Top, 0, 0);
            mapScale.MaxWidth = ScreenLayout.Clamp(width * 0.12, 80, 140);
        }

        if (topRight is not null)
        {
            topRight.Margin = new Thickness(0, insets.Top, insets.Right, 0);
            topRight.MaxHeight = Math.Max(120, height - insets.Top - insets.Bottom);
        }

        if (telemetry is not null)
        {
            telemetry.Margin = new Thickness(0, 0, insets.Right, insets.Bottom);
        }

        if (pip is not null)
        {
            pip.Margin = new Thickness(insets.Left - (toolStrip?.Width ?? 0), 0, 0, insets.Bottom);
        }
    }

    private static Thickness BuildOverlayInsets(Border? toolStrip, StackPanel? topRight, Border? telemetry, Border? pip, GuidedValueSlider? slider, Grid? vjPanel)
    {
        var leftInset = (toolStrip?.Width ?? ScreenMetrics.ToolStripWidth) + ScreenMetrics.ToolsMargin;
        var rightInset = Math.Max(topRight?.Bounds.Width ?? 0, slider?.IsVisible == true ? slider.Bounds.Width : 0) + ScreenMetrics.ToolsMargin;
        var bottomInset = Math.Max(Math.Max(telemetry?.Bounds.Height ?? 0, pip?.Bounds.Height ?? 0), vjPanel?.IsVisible == true ? vjPanel.Bounds.Height : 0) + ScreenMetrics.ToolsMargin;
        var topInset = ScreenMetrics.ToolsMargin;
        return new Thickness(leftInset, topInset, rightInset, bottomInset);
    }

    private void SetStickLayout(string side, double stickSize, double thumbSize)
    {
        var panel = this.FindControl<Border>($"{side}StickPanel");
        if (panel is not null)
        {
            panel.Height = stickSize;
            panel.Margin = new Thickness(ScreenMetrics.LayoutMargin, 0);
        }

        SetSquare($"{side}StickOuterRing", stickSize * 0.78);
        SetSquare($"{side}StickInnerRing", stickSize * 0.39);
        SetSquare($"{side}StickThumb", thumbSize, cornerRadiusRatio: 0.5);
    }

    private void SetSize(string name, double width, double height)
    {
        var control = this.FindControl<Control>(name);
        if (control is null) return;
        control.Width = width;
        control.Height = height;
    }

    private void SetSquare(string name, double size, double? cornerRadiusRatio = null)
    {
        SetSize(name, size, size);
        if (cornerRadiusRatio.HasValue && this.FindControl<Border>(name) is { } border)
        {
            border.CornerRadius = new CornerRadius(size * cornerRadiusRatio.Value);
        }
    }

    private void OnBrandButtonClicked(object? sender, RoutedEventArgs e)
    {
        FindShell()?.ShowToolDrawerCommand.Execute().Subscribe();
    }

    private void OnToggleVirtualJoystick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FlyViewModel vm)
        {
            vm.ToggleVirtualJoystick();
        }
    }

    private void OnDelayButtonActivated(object? sender, EventArgs e)
    {
        if (DataContext is FlyViewModel vm)
        {
            _ = vm.ConfirmGuidedActionAsync();
        }
    }

    private void OpenIndicatorDrawer(IndicatorDrawerKind kind, Control? indicator = null)
    {
        var shell = FindShell();
        shell?.OpenIndicatorDrawer(kind, GetIndicatorAnchorX(indicator));
    }

    private ShellViewModel? FindShell()
    {
        return this.GetVisualAncestors().OfType<Control>().Select(control => control.DataContext).OfType<ShellViewModel>().FirstOrDefault();
    }

    private double GetIndicatorAnchorX(Control? indicator)
    {
        if (indicator is null) return ScreenMetrics.StandardMargin;
        var point = indicator.TranslatePoint(new Point(indicator.Bounds.Width / 2, 0), this);
        return point?.X ?? ScreenMetrics.StandardMargin;
    }

    private void OnToolbarIndicatorClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: IndicatorDrawerKind kind } button)
        {
            OpenIndicatorDrawer(kind, button);
        }
    }

    private void OnMoreActionsClicked(object? sender, RoutedEventArgs e)
    {
        // More actions menu — reserved for extended guided actions.
    }

    private void OnSwapPip(object? sender, RoutedEventArgs e)
    {
        // PiP swap is a view-level toggle; no ViewModel action needed yet.
    }

    private void OnShowPreflightPopup(object? sender, RoutedEventArgs e)
    {
        var popup = this.FindControl<Border>("PreflightPopup");
        if (popup is not null) popup.IsVisible = true;
    }

    private void OnClosePreflightPopup(object? sender, RoutedEventArgs e)
    {
        var popup = this.FindControl<Border>("PreflightPopup");
        if (popup is not null) popup.IsVisible = false;
    }

    private void OnDismissCriticalMessage(object? sender, RoutedEventArgs e)
    {
        var popup = this.FindControl<Border>("CriticalMessagePopup");
        if (popup is not null) popup.IsVisible = false;
    }

    private void OnDismissMissionComplete(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FlyViewModel vm)
        {
            vm.DismissMissionComplete();
        }
    }

    public void ShowCriticalMessage(string message)
    {
        var popup = this.FindControl<Border>("CriticalMessagePopup");
        var text = this.FindControl<TextBlock>("CriticalMessageText");
        if (popup is not null) popup.IsVisible = true;
        if (text is not null) text.Text = message;
    }

    public void ShowVehicleWarning(string message)
    {
        var banner = this.FindControl<Border>("VehicleWarningBanner");
        var text = this.FindControl<TextBlock>("VehicleWarningText");
        if (banner is not null) banner.IsVisible = true;
        if (text is not null) text.Text = message;
    }

    public void DismissVehicleWarning()
    {
        var banner = this.FindControl<Border>("VehicleWarningBanner");
        if (banner is not null) banner.IsVisible = false;
    }

    public void ShowAltitudeSlider(bool show)
    {
        var slider = this.FindControl<GuidedValueSlider>("AltitudeSlider");
        if (slider is not null) slider.IsVisible = show;
    }

    private void OnLeftStickPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Panel panel)
        {
            e.Pointer.Capture(panel);
            UpdateLeftStick(panel, e.GetPosition(panel));
            e.Handled = true;
        }
    }

    private void OnLeftStickMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Panel panel && e.Pointer.Captured == panel)
        {
            UpdateLeftStick(panel, e.GetPosition(panel));
            e.Handled = true;
        }
    }

    private void OnLeftStickReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Panel panel)
        {
            e.Pointer.Capture(null);
            if (DataContext is FlyViewModel vm)
            {
                vm.ReleaseVirtualJoystickLeft();
            }

            var thumb = this.FindControl<Border>("LeftStickThumb");
            if (thumb is not null)
            {
                thumb.Margin = new Thickness(0);
            }

            e.Handled = true;
        }
    }

    private void OnRightStickPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Panel panel)
        {
            e.Pointer.Capture(panel);
            UpdateRightStick(panel, e.GetPosition(panel));
            e.Handled = true;
        }
    }

    private void OnRightStickMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Panel panel && e.Pointer.Captured == panel)
        {
            UpdateRightStick(panel, e.GetPosition(panel));
            e.Handled = true;
        }
    }

    private void OnRightStickReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Panel panel)
        {
            e.Pointer.Capture(null);
            if (DataContext is FlyViewModel vm)
            {
                vm.ReleaseVirtualJoystickRight();
            }

            var thumb = this.FindControl<Border>("RightStickThumb");
            if (thumb is not null)
            {
                thumb.Margin = new Thickness(0);
            }

            e.Handled = true;
        }
    }

    private void UpdateLeftStick(Panel panel, Point position)
    {
        var (x, y) = NormalizeStickPosition(panel, position);
        if (DataContext is FlyViewModel vm)
        {
            vm.UpdateVirtualJoystickLeft(x, y);
        }

        var thumb = this.FindControl<Border>("LeftStickThumb");
        if (thumb is not null)
        {
            var maxOffset = Math.Min(panel.Bounds.Width, panel.Bounds.Height) / 2 - 25;
            thumb.Margin = new Thickness(x * maxOffset, -y * maxOffset, -x * maxOffset, y * maxOffset);
        }
    }

    private void UpdateRightStick(Panel panel, Point position)
    {
        var (x, y) = NormalizeStickPosition(panel, position);
        if (DataContext is FlyViewModel vm)
        {
            vm.UpdateVirtualJoystickRight(x, y);
        }

        var thumb = this.FindControl<Border>("RightStickThumb");
        if (thumb is not null)
        {
            var maxOffset = Math.Min(panel.Bounds.Width, panel.Bounds.Height) / 2 - 25;
            thumb.Margin = new Thickness(x * maxOffset, -y * maxOffset, -x * maxOffset, y * maxOffset);
        }
    }

    private static (double X, double Y) NormalizeStickPosition(Panel panel, Point position)
    {
        var centerX = panel.Bounds.Width / 2;
        var centerY = panel.Bounds.Height / 2;
        var radius = Math.Min(centerX, centerY) - 25;
        if (radius <= 0) return (0, 0);

        var dx = (position.X - centerX) / radius;
        var dy = -(position.Y - centerY) / radius; // Invert Y: up is positive

        // Clamp to unit circle
        var magnitude = Math.Sqrt(dx * dx + dy * dy);
        if (magnitude > 1)
        {
            dx /= magnitude;
            dy /= magnitude;
        }

        return (dx, dy);
    }
}
