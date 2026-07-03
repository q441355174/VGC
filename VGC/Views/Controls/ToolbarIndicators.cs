using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using System.Globalization;
using System.Linq;

namespace VGC.Views.Controls;

/// <summary>
/// Base class for all QGC-style toolbar indicators.
/// Each indicator shows a compact status in the toolbar and can expand to show details.
/// Equivalent to QGC's individual indicator QML files.
/// </summary>
public abstract class ToolbarIndicatorBase : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ToolbarIndicatorBase, string>(nameof(Label), "");

    public static readonly StyledProperty<string> ValueTextProperty =
        AvaloniaProperty.Register<ToolbarIndicatorBase, string>(nameof(ValueText), "—");

    public static readonly StyledProperty<bool> IsHealthyProperty =
        AvaloniaProperty.Register<ToolbarIndicatorBase, bool>(nameof(IsHealthy), true);

    public static readonly StyledProperty<bool> ShowIndicatorProperty =
        AvaloniaProperty.Register<ToolbarIndicatorBase, bool>(nameof(ShowIndicator), true);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public bool IsHealthy { get => GetValue(IsHealthyProperty); set => SetValue(IsHealthyProperty, value); }
    public bool ShowIndicator { get => GetValue(ShowIndicatorProperty); set => SetValue(ShowIndicatorProperty, value); }

    public event EventHandler? IndicatorClicked;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        IndicatorClicked?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}

/// <summary>
/// GPS status indicator — shows fix type, satellite count, icon color.
/// Equivalent to QGC Toolbar/GPSIndicator.qml
/// </summary>
public sealed class GpsToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<int> FixTypeProperty =
        AvaloniaProperty.Register<GpsToolbarIndicator, int>(nameof(FixType));

    public static readonly StyledProperty<int> SatelliteCountProperty =
        AvaloniaProperty.Register<GpsToolbarIndicator, int>(nameof(SatelliteCount));

    public int FixType { get => GetValue(FixTypeProperty); set => SetValue(FixTypeProperty, value); }
    public int SatelliteCount { get => GetValue(SatelliteCountProperty); set => SetValue(SatelliteCountProperty, value); }

    public GpsToolbarIndicator()
    {
        Label = "GPS";
    }
}

/// <summary>
/// Battery indicator — shows voltage, percentage, colored warning.
/// Equivalent to QGC Toolbar/BatteryIndicator.qml
/// </summary>
public sealed class BatteryToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<double> VoltageProperty =
        AvaloniaProperty.Register<BatteryToolbarIndicator, double>(nameof(Voltage));

    public static readonly StyledProperty<int> PercentRemainingProperty =
        AvaloniaProperty.Register<BatteryToolbarIndicator, int>(nameof(PercentRemaining), -1);

    public double Voltage { get => GetValue(VoltageProperty); set => SetValue(VoltageProperty, value); }
    public int PercentRemaining { get => GetValue(PercentRemainingProperty); set => SetValue(PercentRemainingProperty, value); }

    public BatteryToolbarIndicator()
    {
        Label = "BAT";
    }
}

/// <summary>
/// Armed status indicator.
/// Equivalent to QGC Toolbar/ArmedIndicator.qml
/// </summary>
public sealed class ArmedToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<bool> IsArmedProperty =
        AvaloniaProperty.Register<ArmedToolbarIndicator, bool>(nameof(IsArmed));

    public bool IsArmed { get => GetValue(IsArmedProperty); set => SetValue(IsArmedProperty, value); }

    public ArmedToolbarIndicator()
    {
        Label = "ARM";
    }
}

/// <summary>
/// Flight mode indicator with clickable mode selection.
/// Equivalent to QGC Toolbar/FlightModeIndicator.qml
/// </summary>
public sealed class FlightModeToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<string> ModeNameProperty =
        AvaloniaProperty.Register<FlightModeToolbarIndicator, string>(nameof(ModeName), "—");

    public string ModeName { get => GetValue(ModeNameProperty); set => SetValue(ModeNameProperty, value); }

    public FlightModeToolbarIndicator()
    {
        Label = "MODE";
    }
}

/// <summary>
/// RC signal strength indicator.
/// Equivalent to QGC Toolbar/RCRSSIIndicator.qml
/// </summary>
public sealed class RcRssiToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<int> RssiPercentProperty =
        AvaloniaProperty.Register<RcRssiToolbarIndicator, int>(nameof(RssiPercent), -1);

    public int RssiPercent { get => GetValue(RssiPercentProperty); set => SetValue(RssiPercentProperty, value); }

    public RcRssiToolbarIndicator()
    {
        Label = "RC";
    }
}

/// <summary>
/// Telemetry RSSI indicator.
/// Equivalent to QGC Toolbar/TelemetryRSSIIndicator.qml
/// </summary>
public sealed class TelemetryRssiToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<int> RssiPercentProperty =
        AvaloniaProperty.Register<TelemetryRssiToolbarIndicator, int>(nameof(RssiPercent), -1);

    public int RssiPercent { get => GetValue(RssiPercentProperty); set => SetValue(RssiPercentProperty, value); }

    public TelemetryRssiToolbarIndicator()
    {
        Label = "TEL";
    }
}

/// <summary>
/// Message count indicator with severity coloring.
/// Equivalent to QGC Toolbar/MessageIndicator.qml
/// </summary>
public sealed class MessageToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<int> MessageCountProperty =
        AvaloniaProperty.Register<MessageToolbarIndicator, int>(nameof(MessageCount));

    public static readonly StyledProperty<int> HighestSeverityProperty =
        AvaloniaProperty.Register<MessageToolbarIndicator, int>(nameof(HighestSeverity), 6);

    public int MessageCount { get => GetValue(MessageCountProperty); set => SetValue(MessageCountProperty, value); }
    public int HighestSeverity { get => GetValue(HighestSeverityProperty); set => SetValue(HighestSeverityProperty, value); }

    public MessageToolbarIndicator()
    {
        Label = "MSG";
    }
}

/// <summary>
/// Main status indicator — combined vehicle overview with brand gradient.
/// Equivalent to QGC Toolbar/MainStatusIndicator.qml
/// Left-most toolbar element with purple gradient background.
/// </summary>
public sealed class MainStatusToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<string> FirmwareTextProperty =
        AvaloniaProperty.Register<MainStatusToolbarIndicator, string>(nameof(FirmwareText), "");

    public static readonly StyledProperty<string> VehicleTypeTextProperty =
        AvaloniaProperty.Register<MainStatusToolbarIndicator, string>(nameof(VehicleTypeText), "");

    public string FirmwareText { get => GetValue(FirmwareTextProperty); set => SetValue(FirmwareTextProperty, value); }
    public string VehicleTypeText { get => GetValue(VehicleTypeTextProperty); set => SetValue(VehicleTypeTextProperty, value); }

    public MainStatusToolbarIndicator()
    {
        Label = "VGC";
    }
}

/// <summary>
/// Indicator detail expansion page — shown when an indicator is clicked.
/// Equivalent to QGC Toolbar/ToolIndicatorPage.qml
/// Two-column layout: normal content (left) + expanded content (right, behind divider).
/// </summary>
public sealed class ToolIndicatorPage : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ToolIndicatorPage, string>(nameof(Title), "");

    public static readonly StyledProperty<object?> ExpandedContentProperty =
        AvaloniaProperty.Register<ToolIndicatorPage, object?>(nameof(ExpandedContent));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ToolIndicatorPage, bool>(nameof(IsExpanded), false);

    public static readonly StyledProperty<bool> WaitForParametersProperty =
        AvaloniaProperty.Register<ToolIndicatorPage, bool>(nameof(WaitForParameters), false);

    public static readonly StyledProperty<string> WaitingTextProperty =
        AvaloniaProperty.Register<ToolIndicatorPage, string>(nameof(WaitingText), "Waiting for parameters...");

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public object? ExpandedContent { get => GetValue(ExpandedContentProperty); set => SetValue(ExpandedContentProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool WaitForParameters { get => GetValue(WaitForParametersProperty); set => SetValue(WaitForParametersProperty, value); }
    public string WaitingText { get => GetValue(WaitingTextProperty); set => SetValue(WaitingTextProperty, value); }
}

/// <summary>
/// ToolStrip — vertical icon button strip anchored left side.
/// Equivalent to QGC QmlControls/ToolStrip.qml
/// Width: 7 * DefaultFontPixelWidth (70px), square buttons, flickable.
/// </summary>
public sealed class ToolStripControl : Control
{
    public static readonly new StyledProperty<double> MaxHeightProperty =
        AvaloniaProperty.Register<ToolStripControl, double>("ToolStripMaxHeight", double.PositiveInfinity);

    public new double MaxHeight { get => GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty, value); }

    static ToolStripControl()
    {
        AffectsRender<ToolStripControl>(MaxHeightProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(ScreenMetrics.ToolStripWidth, Math.Min(availableSize.Height, MaxHeight));
    }
}

/// <summary>
/// ToolStripHoverButton — square icon+text button for ToolStrip.
/// Equivalent to QGC QmlControls/ToolStripHoverButton.qml
/// Width = parent strip width, height = width (square), icon above text.
/// </summary>
public class ToolStripHoverButton : Button
{
    public static readonly StyledProperty<string> IconTextProperty =
        AvaloniaProperty.Register<ToolStripHoverButton, string>(nameof(IconText), "");

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<ToolStripHoverButton, bool>(nameof(IsChecked), false);

    public string IconText { get => GetValue(IconTextProperty); set => SetValue(IconTextProperty, value); }
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }
}

/// <summary>
/// QGCDelayButton — hold-to-confirm button with circular progress.
/// Equivalent to QGC's QGCDelayButton / GuidedActionConfirm.
/// User must hold the button for ~1 second to activate.
/// </summary>
public class QgcDelayButton : Button
{
    public static readonly StyledProperty<double> DelaySecondsProperty =
        AvaloniaProperty.Register<QgcDelayButton, double>(nameof(DelaySeconds), 1.0);

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<QgcDelayButton, double>(nameof(Progress), 0);

    public double DelaySeconds { get => GetValue(DelaySecondsProperty); set => SetValue(DelaySecondsProperty, value); }
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public event EventHandler? Activated;

    private DateTime? _pressStart;
    private bool _isHolding;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressStart = DateTime.Now;
        _isHolding = true;
        e.Pointer.Capture(this);
        _ = TrackHoldAsync();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isHolding = false;
        _pressStart = null;
        Progress = 0;
        e.Pointer.Capture(null);
    }

    private async Task TrackHoldAsync()
    {
        while (_isHolding && _pressStart.HasValue)
        {
            var elapsed = (DateTime.Now - _pressStart.Value).TotalSeconds;
            Progress = Math.Clamp(elapsed / DelaySeconds, 0, 1);

            if (Progress >= 1.0)
            {
                _isHolding = false;
                Activated?.Invoke(this, EventArgs.Empty);
                Progress = 0;
                return;
            }

            await Task.Delay(30).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// PreFlightCheckButton — check item with left-side status flag (green/orange/red).
/// Equivalent to QGC QmlControls/PreFlightCheckButton.qml
/// Width: 40 * DefaultFontPixelWidth, left color strip indicates status.
/// </summary>
public class PreFlightCheckButtonControl : Control
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PreFlightCheckButtonControl, string>(nameof(Text), "");

    public static readonly StyledProperty<PreFlightCheckState> StateProperty =
        AvaloniaProperty.Register<PreFlightCheckButtonControl, PreFlightCheckState>(nameof(State));

    public static readonly StyledProperty<string> FailureTextProperty =
        AvaloniaProperty.Register<PreFlightCheckButtonControl, string>(nameof(FailureText), "");

    static PreFlightCheckButtonControl()
    {
        AffectsRender<PreFlightCheckButtonControl>(TextProperty, StateProperty, FailureTextProperty);
    }

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public PreFlightCheckState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public string FailureText { get => GetValue(FailureTextProperty); set => SetValue(FailureTextProperty, value); }

    public event EventHandler? Toggled;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 10 || bounds.Height < 10) return;

        var w = bounds.Width;
        var h = bounds.Height;
        var flagWidth = ScreenMetrics.DefaultFontPixelWidth * 4;

        // Status flag (left strip)
        var flagColor = State switch
        {
            PreFlightCheckState.Passed => QgcColors.ColorGreen,
            PreFlightCheckState.Pending => QgcColors.ColorOrange,
            PreFlightCheckState.Failed => QgcColors.ColorRed,
            _ => QgcColors.ColorGrey
        };
        context.DrawRectangle(new SolidColorBrush(flagColor), null,
            new Rect(0, 0, flagWidth, h), 3, 3);

        // Background
        context.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
            new Rect(flagWidth, 0, w - flagWidth, h), 0, 3);

        // Text
        var typeface = new Typeface("Segoe UI");
        var displayText = State == PreFlightCheckState.Failed && !string.IsNullOrEmpty(FailureText)
            ? FailureText : Text;
        var textBrush = State == PreFlightCheckState.Failed
            ? new SolidColorBrush(QgcColors.WarningText)
            : new SolidColorBrush(QgcColors.ButtonText);
        var fmt = new FormattedText(displayText, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 13, textBrush);
        context.DrawText(fmt, new Point(flagWidth + 12, (h - fmt.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Toggled?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width)
            ? ScreenMetrics.DefaultFontPixelWidth * 40
            : availableSize.Width;
        return new Size(w, 36);
    }
}

public enum PreFlightCheckState
{
    Pending,
    Passed,
    Failed
}

// ════════════════════════════════════════════════════════════════
// SIGNAL STRENGTH
// QGC equivalent: Toolbar/SignalStrength.qml
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Five-bar signal strength indicator, 0–100%.
/// Equivalent to QGC Toolbar/SignalStrength.qml
/// Used inside RC and Telemetry toolbar indicators.
/// </summary>
public sealed class SignalStrength : Control
{
    public static readonly StyledProperty<double> PercentProperty =
        AvaloniaProperty.Register<SignalStrength, double>(nameof(Percent));

    static SignalStrength()
    {
        AffectsRender<SignalStrength>(PercentProperty);
    }

    public double Percent
    {
        get => GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 8 || h < 8) return;

        const int bars = 5;
        var gap = 1.5;
        var barWidth = (w - (bars - 1) * gap) / bars;

        // 0/1/2/3/4/5 active bars matching QGC thresholds: <20/<40/<60/<80/<95/>=95
        var activeBars = Percent < 20 ? 0 :
                         Percent < 40 ? 1 :
                         Percent < 60 ? 2 :
                         Percent < 80 ? 3 :
                         Percent < 95 ? 4 : 5;

        for (var i = 0; i < bars; i++)
        {
            var barH = h * (i + 1) / bars;
            var x    = i * (barWidth + gap);
            var y    = h - barH;
            var brush = i < activeBars
                ? new SolidColorBrush(QgcColors.ColorGreen)
                : new SolidColorBrush(Color.Parse("#3a4a56"));
            context.DrawRectangle(brush, null, new Rect(x, y, barWidth, barH), 1, 1);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var s = double.IsInfinity(availableSize.Width) ? 28 : Math.Min(availableSize.Width, 28);
        return new Size(s, s);
    }
}

// ════════════════════════════════════════════════════════════════
// GIMBAL INDICATOR
// QGC equivalent: Toolbar/GimbalIndicator.qml
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Gimbal status indicator — shows pitch / yaw and lock state.
/// Set ShowIndicator=true when a gimbal is detected on the active vehicle.
/// Equivalent to QGC Toolbar/GimbalIndicator.qml
/// </summary>
public sealed class GimbalToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<double> PitchDegreesProperty =
        AvaloniaProperty.Register<GimbalToolbarIndicator, double>(nameof(PitchDegrees));

    public static readonly StyledProperty<double> YawDegreesProperty =
        AvaloniaProperty.Register<GimbalToolbarIndicator, double>(nameof(YawDegrees));

    public static readonly StyledProperty<bool> IsYawLockedProperty =
        AvaloniaProperty.Register<GimbalToolbarIndicator, bool>(nameof(IsYawLocked));

    public static readonly StyledProperty<bool> IsRetractedProperty =
        AvaloniaProperty.Register<GimbalToolbarIndicator, bool>(nameof(IsRetracted));

    public double PitchDegrees { get => GetValue(PitchDegreesProperty); set => SetValue(PitchDegreesProperty, value); }
    public double YawDegrees   { get => GetValue(YawDegreesProperty);   set => SetValue(YawDegreesProperty, value); }
    public bool IsYawLocked    { get => GetValue(IsYawLockedProperty);  set => SetValue(IsYawLockedProperty, value); }
    public bool IsRetracted    { get => GetValue(IsRetractedProperty);  set => SetValue(IsRetractedProperty, value); }

    public GimbalToolbarIndicator()
    {
        Label = "GBL";
        ShowIndicator = false; // hidden until a gimbal is present on active vehicle
    }

    /// <summary>Status string matching QGC: Retracted / Yaw locked / Yaw follow.</summary>
    public string StatusText =>
        IsRetracted ? "Retracted" :
        IsYawLocked ? "Yaw locked" : "Yaw follow";
}

// ════════════════════════════════════════════════════════════════
// JOYSTICK INDICATOR
// QGC equivalent: Toolbar/JoystickIndicator.qml
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Joystick presence indicator — orange when joystick detected but not enabled for vehicle.
/// Set ShowIndicator=true when a joystick/gamepad is connected.
/// Equivalent to QGC Toolbar/JoystickIndicator.qml
/// </summary>
public sealed class JoystickToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<bool> IsEnabledForVehicleProperty =
        AvaloniaProperty.Register<JoystickToolbarIndicator, bool>(nameof(IsEnabledForVehicle));

    public bool IsEnabledForVehicle
    {
        get => GetValue(IsEnabledForVehicleProperty);
        set => SetValue(IsEnabledForVehicleProperty, value);
    }

    public JoystickToolbarIndicator()
    {
        Label = "JS";
        ShowIndicator = false; // hidden until a joystick is connected
    }
}

// ════════════════════════════════════════════════════════════════
// REMOTE ID INDICATOR
// QGC equivalent: Toolbar/RemoteIDIndicator.qml
// ════════════════════════════════════════════════════════════════

/// <summary>Health state of the Remote ID subsystem.</summary>
public enum RemoteIdState
{
    /// <summary>All checks pass — green.</summary>
    Healthy,
    /// <summary>GPS or BasicID flag missing — yellow.</summary>
    Warning,
    /// <summary>Comms/arm failure or emergency declared — red.</summary>
    Error,
    /// <summary>No RID module available — grey.</summary>
    Unavailable
}

/// <summary>
/// Remote ID health indicator — color-coded icon (green/yellow/red/grey).
/// Set ShowIndicator=true when remoteIDManager.available is true.
/// Equivalent to QGC Toolbar/RemoteIDIndicator.qml
/// </summary>
public sealed class RemoteIdToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<RemoteIdState> RidStateProperty =
        AvaloniaProperty.Register<RemoteIdToolbarIndicator, RemoteIdState>(nameof(RidState), RemoteIdState.Unavailable);

    public static readonly StyledProperty<bool> IsGpsGoodProperty =
        AvaloniaProperty.Register<RemoteIdToolbarIndicator, bool>(nameof(IsGpsGood));

    public static readonly StyledProperty<bool> IsBasicIdGoodProperty =
        AvaloniaProperty.Register<RemoteIdToolbarIndicator, bool>(nameof(IsBasicIdGood));

    public static readonly StyledProperty<bool> IsCommsGoodProperty =
        AvaloniaProperty.Register<RemoteIdToolbarIndicator, bool>(nameof(IsCommsGood));

    public static readonly StyledProperty<bool> IsArmStatusGoodProperty =
        AvaloniaProperty.Register<RemoteIdToolbarIndicator, bool>(nameof(IsArmStatusGood));

    public static readonly StyledProperty<bool> IsEmergencyDeclaredProperty =
        AvaloniaProperty.Register<RemoteIdToolbarIndicator, bool>(nameof(IsEmergencyDeclared));

    public RemoteIdState RidState         { get => GetValue(RidStateProperty);          set => SetValue(RidStateProperty, value); }
    public bool IsGpsGood                 { get => GetValue(IsGpsGoodProperty);          set => SetValue(IsGpsGoodProperty, value); }
    public bool IsBasicIdGood             { get => GetValue(IsBasicIdGoodProperty);      set => SetValue(IsBasicIdGoodProperty, value); }
    public bool IsCommsGood               { get => GetValue(IsCommsGoodProperty);        set => SetValue(IsCommsGoodProperty, value); }
    public bool IsArmStatusGood           { get => GetValue(IsArmStatusGoodProperty);    set => SetValue(IsArmStatusGoodProperty, value); }
    public bool IsEmergencyDeclared       { get => GetValue(IsEmergencyDeclaredProperty); set => SetValue(IsEmergencyDeclaredProperty, value); }

    public RemoteIdToolbarIndicator()
    {
        Label = "RID";
        ShowIndicator = false; // hidden until RID module is available
    }

    /// <summary>
    /// Recompute RidState from individual flag properties.
    /// Mirrors QGC's getRemoteIDState() logic:
    ///   emergency | !comms | !arm → Error
    ///   !gps | !basicId          → Warning
    ///   otherwise                → Healthy
    /// </summary>
    public void RecalculateState()
    {
        RidState = IsEmergencyDeclared || !IsCommsGood || !IsArmStatusGood
            ? RemoteIdState.Error
            : !IsGpsGood || !IsBasicIdGood
                ? RemoteIdState.Warning
                : RemoteIdState.Healthy;

        IsHealthy = RidState == RemoteIdState.Healthy;
    }
}

// ════════════════════════════════════════════════════════════════
// TOOL STRIP DROP PANEL
// QGC equivalent: QmlControls/ToolStripDropPanel.qml
// Floating panel with left-pointing arrow attached to ToolStrip button.
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Floating drop-panel that emerges from the ToolStrip with a left-pointing arrow.
/// The arrow points back toward the button that opened it; content is arbitrary.
/// Equivalent to QGC QmlControls/ToolStripDropPanel.qml
/// </summary>
public sealed class ToolStripDropPanel : ContentControl
{
    public static readonly StyledProperty<double> ArrowOffsetYProperty =
        AvaloniaProperty.Register<ToolStripDropPanel, double>(nameof(ArrowOffsetY), 20);

    public double ArrowOffsetY
    {
        get => GetValue(ArrowOffsetYProperty);
        set => SetValue(ArrowOffsetYProperty, value);
    }

    // Arrow dimensions matching QGC: base = radius, point = radius * 0.666
    private double ArrowBaseH => ScreenMetrics.DefaultFontPixelHeight * 1.25;
    private double ArrowPtW   => ArrowBaseH * 0.666;
    private double DropMargin => ScreenMetrics.DefaultFontPixelWidth;

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 10 || h < 10) return;

        var ptW  = ArrowPtW;
        var ptH  = ArrowBaseH;
        var arrowY = Math.Clamp(ArrowOffsetY, ptH / 2, h - ptH / 2);

        // Panel body (right of arrow point)
        var bodyBrush = new SolidColorBrush(QgcColors.WindowShade);

        // Build complete shape: panel rect with arrow notch on left side
        var geo = new StreamGeometry();
        using (var sctx = geo.Open())
        {
            // Start at top-left of panel body
            sctx.BeginFigure(new Point(ptW, 0), isFilled: true);
            sctx.LineTo(new Point(w, 0));           // top edge →
            sctx.LineTo(new Point(w, h));           // right edge ↓
            sctx.LineTo(new Point(ptW, h));         // bottom edge ←
            sctx.LineTo(new Point(ptW, arrowY + ptH / 2));  // down to arrow base bottom
            sctx.LineTo(new Point(0,   arrowY));            // ← arrow point
            sctx.LineTo(new Point(ptW, arrowY - ptH / 2));  // up to arrow base top
            sctx.LineTo(new Point(ptW, 0));         // back to start
            sctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(bodyBrush, null, geo);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Reserve space for the arrow point on the left
        var ptW   = ArrowPtW;
        var inner = base.MeasureOverride(availableSize.WithWidth(
            double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width - ptW));
        return inner.WithWidth(inner.Width + ptW);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Offset content right by arrow width + margin so it doesn't overlap the arrow
        var ptW = ArrowPtW + DropMargin;
        foreach (var child in LogicalChildren.OfType<Control>())
        {
            child.Arrange(new Rect(ptW, DropMargin,
                Math.Max(0, finalSize.Width  - ptW    - DropMargin),
                Math.Max(0, finalSize.Height - DropMargin * 2)));
        }
        return finalSize;
    }
}

// ════════════════════════════════════════════════════════════════
// MULTI-VEHICLE LIST
// QGC equivalent: FlyView/MultiVehicleList.qml
// Scrollable list of connected vehicles with ID / mode / arm state.
// ════════════════════════════════════════════════════════════════

/// <summary>Minimal vehicle summary used by MultiVehicleList.</summary>
public sealed record VehicleSummary(
    int    VehicleId,
    string FlightMode,
    bool   IsArmed,
    bool   IsActive,
    double RollDeg  = 0,
    double PitchDeg = 0,
    double HeadingDeg = 0);

/// <summary>
/// Scrollable list of connected vehicles — shows ID, flight mode, arm state.
/// Clicking a row fires <see cref="VehicleSelected"/> with the vehicle ID.
/// Equivalent to QGC FlyView/MultiVehicleList.qml
/// </summary>
public sealed class MultiVehicleList : Control
{
    public static readonly StyledProperty<IReadOnlyList<VehicleSummary>?> VehiclesProperty =
        AvaloniaProperty.Register<MultiVehicleList, IReadOnlyList<VehicleSummary>?>(nameof(Vehicles));

    static MultiVehicleList()
    {
        AffectsRender<MultiVehicleList>(VehiclesProperty);
    }

    public IReadOnlyList<VehicleSummary>? Vehicles
    {
        get => GetValue(VehiclesProperty);
        set => SetValue(VehiclesProperty, value);
    }

    public event EventHandler<int>? VehicleSelected;

    private int _hoverIndex = -1;

    private double RowHeight => ScreenMetrics.DefaultFontPixelHeight * 2.8;

    public override void Render(DrawingContext context)
    {
        var vehicles = Vehicles;
        if (vehicles is null || vehicles.Count == 0) return;

        var w  = Bounds.Width;
        var tf = new Typeface("Segoe UI");
        var tfBold = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var rh = RowHeight;

        for (var i = 0; i < vehicles.Count; i++)
        {
            var v  = vehicles[i];
            var y  = i * (rh + 2);
            var isHover = i == _hoverIndex;

            // Row background — highlight active vehicle in green, hover in primary
            var bgColor = v.IsActive  ? Color.Parse("#1a5c2a") :
                          isHover     ? QgcColors.PrimaryButtonFill : QgcColors.ButtonFill;
            var borderPen = v.IsActive ? new Pen(new SolidColorBrush(QgcColors.ColorGreen), 2) : null;
            context.DrawRectangle(new SolidColorBrush(bgColor), borderPen,
                new Rect(1, y, w - 2, rh), 4, 4);

            // Vehicle ID
            var idFmt = new FormattedText($"#{v.VehicleId}", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tfBold,
                ScreenMetrics.DefaultFontPixelHeight * 1.2,
                new SolidColorBrush(Colors.White));
            context.DrawText(idFmt, new Point(10, y + (rh - idFmt.Height) / 2));

            // Flight mode
            var modeFmt = new FormattedText(v.FlightMode, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, ScreenMetrics.DefaultFontPixelHeight,
                new SolidColorBrush(Colors.White));
            context.DrawText(modeFmt, new Point(w / 2 - modeFmt.Width / 2, y + (rh - modeFmt.Height) / 2));

            // Arm state
            var armText  = v.IsArmed ? "Armed" : "Disarmed";
            var armColor = v.IsArmed ? QgcColors.ColorGreen : QgcColors.ColorRed;
            var armFmt = new FormattedText(armText, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(armColor));
            context.DrawText(armFmt, new Point(w - armFmt.Width - 10, y + (rh - armFmt.Height) / 2));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var count = Vehicles?.Count ?? 0;
        var idx = (int)(e.GetPosition(this).Y / (RowHeight + 2));
        _hoverIndex = (idx >= 0 && idx < count) ? idx : -1;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverIndex = -1;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var vehicles = Vehicles;
        if (vehicles is null) return;
        var idx = (int)(e.GetPosition(this).Y / (RowHeight + 2));
        if (idx >= 0 && idx < vehicles.Count)
            VehicleSelected?.Invoke(this, vehicles[idx].VehicleId);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Vehicles?.Count ?? 0;
        var w = double.IsInfinity(availableSize.Width)
            ? ScreenMetrics.DefaultFontPixelWidth * 30
            : availableSize.Width;
        return new Size(w, count * (RowHeight + 2));
    }
}

// ════════════════════════════════════════════════════════════════
// Indicator detail pages (#119-129)
// QGC equivalent: ToolIndicatorPage contentComponent / expandedComponent
// One control per toolbar indicator; each renders a compact
// label → value grid using the shared IndicatorPagePainter helper.
// ════════════════════════════════════════════════════════════════

// ── Shared types ─────────────────────────────────────────────────

/// <summary>A single labeled row displayed inside an indicator detail page.</summary>
public sealed record IndicatorRow(string Label, string Value, Color? Accent = null);

/// <summary>GPS fix / lock type.</summary>
public enum GpsLockType { None, TwoD, ThreeD, Dgps, RtkFloat, RtkFixed }

/// <summary>Per-ESC telemetry entry used by <see cref="EscIndicatorPage"/>.</summary>
public sealed record EscEntry(int Index, int Rpm, double Voltage, double Current,
                               double TemperatureCelsius, int ErrorCount, bool IsOnline);

// ── Shared canvas renderer ────────────────────────────────────────

/// <summary>
/// Shared Canvas renderer for all indicator detail-pages.
/// Draws a compact two-column grid: left=label (secondary colour),
/// right=value (white or an optional accent colour).
/// </summary>
internal static class IndicatorPagePainter
{
    internal const double LabelWidth = 140;
    internal const double RowH       = 22;
    internal const double Gap        = 8;   // top/bottom padding & inter-section gap

    private static readonly Typeface _tf     = new("Segoe UI", FontStyle.Normal, FontWeight.Normal);
    private static readonly Typeface _tfBold = new("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);

    /// <summary>
    /// Draws optional section header + rows into <paramref name="ctx"/> starting at y=Gap.
    /// Returns the final y position.
    /// </summary>
    internal static void Draw(DrawingContext ctx, double w,
        string? header, IReadOnlyList<IndicatorRow> rows)
    {
        var y   = Gap;
        var pen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);

        if (header is not null)
        {
            var hFt = new FormattedText(header, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _tfBold,
                ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(QgcColors.TextSecondary));
            ctx.DrawText(hFt, new Point(Gap, y));
            y += hFt.Height + 3;
            ctx.DrawLine(pen, new Point(Gap, y), new Point(w - Gap, y));
            y += 4;
        }

        foreach (var row in rows)
        {
            var labelFt = new FormattedText(row.Label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _tf,
                ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(QgcColors.TextSecondary));
            var valueFt = new FormattedText(row.Value, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _tfBold,
                ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(row.Accent ?? QgcColors.Text));

            ctx.DrawText(labelFt, new Point(Gap, y + (RowH - labelFt.Height) / 2));
            ctx.DrawText(valueFt, new Point(LabelWidth, y + (RowH - valueFt.Height) / 2));
            y += RowH;
        }
    }

    internal static Size Measure(double availW, int rowCount, bool hasHeader = false)
    {
        var w = double.IsInfinity(availW) ? 280 : availW;
        var headerH = hasHeader ? ScreenMetrics.SmallFontPointSize + 3 + 4 : 0;
        return new Size(w, Gap + headerH + rowCount * RowH + Gap);
    }
}

// ────────────────────────────────────────────────────────────────
// 119. GpsIndicatorPage
//      QGC: Toolbar/GPSIndicatorPage.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Detailed GPS / RTK status shown when the GPS toolbar indicator is tapped.</summary>
public sealed class GpsIndicatorPage : Control
{
    public static readonly StyledProperty<double> HdopProperty =
        AvaloniaProperty.Register<GpsIndicatorPage, double>(nameof(Hdop), double.NaN);
    public static readonly StyledProperty<double> VdopProperty =
        AvaloniaProperty.Register<GpsIndicatorPage, double>(nameof(Vdop), double.NaN);
    public static readonly StyledProperty<double> CourseOverGroundProperty =
        AvaloniaProperty.Register<GpsIndicatorPage, double>(nameof(CourseOverGround), double.NaN);
    public static readonly StyledProperty<int> SatCountProperty =
        AvaloniaProperty.Register<GpsIndicatorPage, int>(nameof(SatCount), 0);
    public static readonly StyledProperty<GpsLockType> LockTypeProperty =
        AvaloniaProperty.Register<GpsIndicatorPage, GpsLockType>(nameof(LockType), GpsLockType.None);
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<GpsIndicatorPage, double>(nameof(Latitude), double.NaN);
    public static readonly StyledProperty<double> LongitudeProperty =
        AvaloniaProperty.Register<GpsIndicatorPage, double>(nameof(Longitude), double.NaN);
    public static readonly StyledProperty<double> AltitudeMProperty =
        AvaloniaProperty.Register<GpsIndicatorPage, double>(nameof(AltitudeM), double.NaN);

    static GpsIndicatorPage()
    {
        AffectsRender<GpsIndicatorPage>(HdopProperty, VdopProperty, CourseOverGroundProperty,
            SatCountProperty, LockTypeProperty, LatitudeProperty, LongitudeProperty, AltitudeMProperty);
        AffectsMeasure<GpsIndicatorPage>(SatCountProperty);
    }

    public double Hdop            { get => GetValue(HdopProperty);            set => SetValue(HdopProperty, value); }
    public double Vdop            { get => GetValue(VdopProperty);            set => SetValue(VdopProperty, value); }
    public double CourseOverGround{ get => GetValue(CourseOverGroundProperty); set => SetValue(CourseOverGroundProperty, value); }
    public int    SatCount        { get => GetValue(SatCountProperty);        set => SetValue(SatCountProperty, value); }
    public GpsLockType LockType   { get => GetValue(LockTypeProperty);        set => SetValue(LockTypeProperty, value); }
    public double Latitude        { get => GetValue(LatitudeProperty);        set => SetValue(LatitudeProperty, value); }
    public double Longitude       { get => GetValue(LongitudeProperty);       set => SetValue(LongitudeProperty, value); }
    public double AltitudeM       { get => GetValue(AltitudeMProperty);       set => SetValue(AltitudeMProperty, value); }

    private static string Fmt(double v, string fmt = "F2") =>
        double.IsNaN(v) ? "–.––" : v.ToString(fmt, CultureInfo.InvariantCulture);

    private static Color LockColor(GpsLockType t) => t switch
    {
        GpsLockType.RtkFixed  => QgcColors.ColorGreen,
        GpsLockType.RtkFloat  => QgcColors.ColorBlue,
        GpsLockType.ThreeD    => QgcColors.ColorBlue,
        GpsLockType.Dgps      => QgcColors.ColorOrange,
        _                     => QgcColors.ColorRed,
    };

    public override void Render(DrawingContext context)
    {
        var lockColor = LockColor(LockType);
        IndicatorPagePainter.Draw(context, Bounds.Width, "GPS",
        [
            new IndicatorRow("Lock",     LockType.ToString(),                  lockColor),
            new IndicatorRow("Sats",     SatCount.ToString(),                  SatCount >= 6 ? QgcColors.ColorGreen : QgcColors.ColorOrange),
            new IndicatorRow("HDOP",     Fmt(Hdop)),
            new IndicatorRow("VDOP",     Fmt(Vdop)),
            new IndicatorRow("COG",      double.IsNaN(CourseOverGround) ? "–" : $"{Fmt(CourseOverGround, "F0")}°"),
            new IndicatorRow("Lat",      Fmt(Latitude, "F6")),
            new IndicatorRow("Lon",      Fmt(Longitude, "F6")),
            new IndicatorRow("Alt",      double.IsNaN(AltitudeM) ? "–" : $"{Fmt(AltitudeM, "F1")} m"),
        ]);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, 8, hasHeader: true);
}

// ────────────────────────────────────────────────────────────────
// 120. BatteryIndicatorPage
//      QGC: FirmwarePlugin/APM|PX4/APMBatteryIndicator.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Detailed battery status shown when the battery toolbar indicator is tapped.</summary>
public sealed class BatteryIndicatorPage : Control
{
    public static readonly StyledProperty<double> VoltageProperty =
        AvaloniaProperty.Register<BatteryIndicatorPage, double>(nameof(Voltage), double.NaN);
    public static readonly StyledProperty<double> CurrentProperty =
        AvaloniaProperty.Register<BatteryIndicatorPage, double>(nameof(Current), double.NaN);
    public static readonly StyledProperty<double> ConsumedMahProperty =
        AvaloniaProperty.Register<BatteryIndicatorPage, double>(nameof(ConsumedMah), double.NaN);
    public static readonly StyledProperty<double> RemainingPercentProperty =
        AvaloniaProperty.Register<BatteryIndicatorPage, double>(nameof(RemainingPercent), double.NaN);
    public static readonly StyledProperty<double> TemperatureProperty =
        AvaloniaProperty.Register<BatteryIndicatorPage, double>(nameof(Temperature), double.NaN);
    public static readonly StyledProperty<int> CellCountProperty =
        AvaloniaProperty.Register<BatteryIndicatorPage, int>(nameof(CellCount), 0);

    static BatteryIndicatorPage()
    {
        AffectsRender<BatteryIndicatorPage>(VoltageProperty, CurrentProperty, ConsumedMahProperty,
            RemainingPercentProperty, TemperatureProperty, CellCountProperty);
    }

    public double Voltage          { get => GetValue(VoltageProperty);          set => SetValue(VoltageProperty, value); }
    public double Current          { get => GetValue(CurrentProperty);          set => SetValue(CurrentProperty, value); }
    public double ConsumedMah      { get => GetValue(ConsumedMahProperty);      set => SetValue(ConsumedMahProperty, value); }
    public double RemainingPercent { get => GetValue(RemainingPercentProperty); set => SetValue(RemainingPercentProperty, value); }
    public double Temperature      { get => GetValue(TemperatureProperty);      set => SetValue(TemperatureProperty, value); }
    public int    CellCount        { get => GetValue(CellCountProperty);        set => SetValue(CellCountProperty, value); }

    private static string Fmt(double v, string fmt = "F2") =>
        double.IsNaN(v) ? "–" : v.ToString(fmt, CultureInfo.InvariantCulture);

    public override void Render(DrawingContext context)
    {
        var pct = RemainingPercent;
        var pctColor = double.IsNaN(pct) ? QgcColors.Text
            : pct < 20 ? QgcColors.ColorRed
            : pct < 40 ? QgcColors.ColorOrange
            : QgcColors.ColorGreen;

        IndicatorPagePainter.Draw(context, Bounds.Width, "Battery",
        [
            new IndicatorRow("Voltage",    double.IsNaN(Voltage)     ? "–" : $"{Fmt(Voltage, "F2")} V"),
            new IndicatorRow("Current",    double.IsNaN(Current)     ? "–" : $"{Fmt(Current, "F1")} A"),
            new IndicatorRow("Consumed",   double.IsNaN(ConsumedMah) ? "–" : $"{Fmt(ConsumedMah, "F0")} mAh"),
            new IndicatorRow("Remaining",  double.IsNaN(pct)         ? "–" : $"{Fmt(pct, "F0")} %", pctColor),
            new IndicatorRow("Cell Count", CellCount > 0 ? $"{CellCount}S" : "–"),
            new IndicatorRow("Temperature", double.IsNaN(Temperature) ? "–" : $"{Fmt(Temperature, "F1")} °C"),
        ]);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, 6, hasHeader: true);
}

// ────────────────────────────────────────────────────────────────
// 121. RCRSSIIndicatorPage
//      QGC: Toolbar/RCRSSIIndicator.qml (content section)
// ────────────────────────────────────────────────────────────────

/// <summary>Detailed RC link status shown when the RC RSSI toolbar indicator is tapped.</summary>
public sealed class RCRSSIIndicatorPage : Control
{
    public static readonly StyledProperty<int> RssiPercentProperty =
        AvaloniaProperty.Register<RCRSSIIndicatorPage, int>(nameof(RssiPercent), -1);
    public static readonly StyledProperty<int> ChannelCountProperty =
        AvaloniaProperty.Register<RCRSSIIndicatorPage, int>(nameof(ChannelCount), 0);
    public static readonly StyledProperty<int> LostCountProperty =
        AvaloniaProperty.Register<RCRSSIIndicatorPage, int>(nameof(LostCount), -1);
    public static readonly StyledProperty<int> ErrorCountProperty =
        AvaloniaProperty.Register<RCRSSIIndicatorPage, int>(nameof(ErrorCount), -1);

    static RCRSSIIndicatorPage() =>
        AffectsRender<RCRSSIIndicatorPage>(RssiPercentProperty, ChannelCountProperty,
                                            LostCountProperty, ErrorCountProperty);

    public int RssiPercent  { get => GetValue(RssiPercentProperty);  set => SetValue(RssiPercentProperty, value); }
    public int ChannelCount { get => GetValue(ChannelCountProperty); set => SetValue(ChannelCountProperty, value); }
    public int LostCount    { get => GetValue(LostCountProperty);    set => SetValue(LostCountProperty, value); }
    public int ErrorCount   { get => GetValue(ErrorCountProperty);   set => SetValue(ErrorCountProperty, value); }

    public override void Render(DrawingContext context)
    {
        var rssi  = RssiPercent;
        var rssiColor = rssi < 0   ? QgcColors.TextSecondary
                      : rssi < 30  ? QgcColors.ColorRed
                      : rssi < 60  ? QgcColors.ColorOrange
                      : QgcColors.ColorGreen;

        IndicatorPagePainter.Draw(context, Bounds.Width, "RC Link",
        [
            new IndicatorRow("RSSI",     rssi < 0 ? "N/A" : $"{rssi} %", rssiColor),
            new IndicatorRow("Channels", ChannelCount > 0 ? ChannelCount.ToString() : "–"),
            new IndicatorRow("Lost",     LostCount  >= 0 ? LostCount.ToString()  : "–",
                             LostCount > 10 ? QgcColors.ColorOrange : null),
            new IndicatorRow("Errors",   ErrorCount >= 0 ? ErrorCount.ToString() : "–",
                             ErrorCount > 0 ? QgcColors.ColorRed : null),
        ]);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, 4, hasHeader: true);
}

// ────────────────────────────────────────────────────────────────
// 122. TelemetryRSSIIndicatorPage
//      QGC: Toolbar/TelemetryRSSIIndicator.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Detailed telemetry radio status shown when the telemetry indicator is tapped.</summary>
public sealed class TelemetryRSSIIndicatorPage : Control
{
    public static readonly StyledProperty<int>  LocalRssiProperty  = AvaloniaProperty.Register<TelemetryRSSIIndicatorPage, int>(nameof(LocalRssi),  -1);
    public static readonly StyledProperty<int>  RemoteRssiProperty = AvaloniaProperty.Register<TelemetryRSSIIndicatorPage, int>(nameof(RemoteRssi), -1);
    public static readonly StyledProperty<int>  NoiseProperty      = AvaloniaProperty.Register<TelemetryRSSIIndicatorPage, int>(nameof(Noise),      -1);
    public static readonly StyledProperty<int>  RemoteNoiseProperty= AvaloniaProperty.Register<TelemetryRSSIIndicatorPage, int>(nameof(RemoteNoise),-1);
    public static readonly StyledProperty<int>  RxErrorsProperty   = AvaloniaProperty.Register<TelemetryRSSIIndicatorPage, int>(nameof(RxErrors),   -1);
    public static readonly StyledProperty<int>  FixedProperty      = AvaloniaProperty.Register<TelemetryRSSIIndicatorPage, int>(nameof(Fixed),      -1);
    public static readonly StyledProperty<int>  TxBufferProperty   = AvaloniaProperty.Register<TelemetryRSSIIndicatorPage, int>(nameof(TxBuffer),   -1);

    static TelemetryRSSIIndicatorPage() =>
        AffectsRender<TelemetryRSSIIndicatorPage>(LocalRssiProperty, RemoteRssiProperty,
            NoiseProperty, RemoteNoiseProperty, RxErrorsProperty, FixedProperty, TxBufferProperty);

    public int LocalRssi   { get => GetValue(LocalRssiProperty);   set => SetValue(LocalRssiProperty, value); }
    public int RemoteRssi  { get => GetValue(RemoteRssiProperty);  set => SetValue(RemoteRssiProperty, value); }
    public int Noise       { get => GetValue(NoiseProperty);       set => SetValue(NoiseProperty, value); }
    public int RemoteNoise { get => GetValue(RemoteNoiseProperty); set => SetValue(RemoteNoiseProperty, value); }
    public int RxErrors    { get => GetValue(RxErrorsProperty);    set => SetValue(RxErrorsProperty, value); }
    public int Fixed       { get => GetValue(FixedProperty);       set => SetValue(FixedProperty, value); }
    public int TxBuffer    { get => GetValue(TxBufferProperty);    set => SetValue(TxBufferProperty, value); }

    private static string I(int v) => v < 0 ? "–" : v.ToString();

    public override void Render(DrawingContext context)
    {
        IndicatorPagePainter.Draw(context, Bounds.Width, "Telemetry",
        [
            new IndicatorRow("Local RSSI",    I(LocalRssi)),
            new IndicatorRow("Remote RSSI",   I(RemoteRssi)),
            new IndicatorRow("Noise",         I(Noise)),
            new IndicatorRow("Remote Noise",  I(RemoteNoise)),
            new IndicatorRow("RX Errors",     I(RxErrors), RxErrors > 0 ? QgcColors.ColorOrange : null),
            new IndicatorRow("Fixed",         I(Fixed)),
            new IndicatorRow("TX Buffer",     I(TxBuffer)),
        ]);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, 7, hasHeader: true);
}

// ────────────────────────────────────────────────────────────────
// 123. MainStatusIndicatorPage
//      QGC: Toolbar/MainStatusIndicatorOfflinePage.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Main vehicle status detail page — shows arming check results.</summary>
public sealed class MainStatusIndicatorPage : Control
{
    public static readonly StyledProperty<string> VehicleStatusProperty =
        AvaloniaProperty.Register<MainStatusIndicatorPage, string>(nameof(VehicleStatus), "Disconnected");
    public static readonly StyledProperty<IReadOnlyList<string>> ArmingMessagesProperty =
        AvaloniaProperty.Register<MainStatusIndicatorPage, IReadOnlyList<string>>(
            nameof(ArmingMessages), Array.Empty<string>());

    static MainStatusIndicatorPage()
    {
        AffectsRender<MainStatusIndicatorPage>(VehicleStatusProperty, ArmingMessagesProperty);
        AffectsMeasure<MainStatusIndicatorPage>(ArmingMessagesProperty);
    }

    public string VehicleStatus
    {
        get => GetValue(VehicleStatusProperty);
        set => SetValue(VehicleStatusProperty, value);
    }

    public IReadOnlyList<string> ArmingMessages
    {
        get => GetValue(ArmingMessagesProperty);
        set => SetValue(ArmingMessagesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var rows = new List<IndicatorRow>
        {
            new IndicatorRow("Status", VehicleStatus,
                VehicleStatus.Contains("Armed", StringComparison.OrdinalIgnoreCase)
                    ? QgcColors.ColorGreen : QgcColors.Text),
        };
        rows.AddRange(ArmingMessages.Select(m =>
            new IndicatorRow("Check", m, QgcColors.ColorOrange)));
        IndicatorPagePainter.Draw(context, Bounds.Width, "Vehicle Status", rows);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, 1 + (ArmingMessages?.Count ?? 0), hasHeader: true);
}

// ────────────────────────────────────────────────────────────────
// 124. FlightModeIndicatorPage
//      QGC: FirmwarePlugin/APM|PX4/APMFlightModeIndicator.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Flight mode detail page — current mode + list of available modes.</summary>
public sealed class FlightModeIndicatorPage : Control
{
    public static readonly StyledProperty<string> CurrentModeProperty =
        AvaloniaProperty.Register<FlightModeIndicatorPage, string>(nameof(CurrentMode), "");
    public static readonly StyledProperty<IReadOnlyList<string>> AvailableModesProperty =
        AvaloniaProperty.Register<FlightModeIndicatorPage, IReadOnlyList<string>>(
            nameof(AvailableModes), Array.Empty<string>());

    static FlightModeIndicatorPage()
    {
        AffectsRender<FlightModeIndicatorPage>(CurrentModeProperty, AvailableModesProperty);
        AffectsMeasure<FlightModeIndicatorPage>(AvailableModesProperty);
    }

    public string CurrentMode
    {
        get => GetValue(CurrentModeProperty);
        set => SetValue(CurrentModeProperty, value);
    }

    public IReadOnlyList<string> AvailableModes
    {
        get => GetValue(AvailableModesProperty);
        set => SetValue(AvailableModesProperty, value);
    }

    public event EventHandler<string>? ModeChangeRequested;

    public override void Render(DrawingContext context)
    {
        var rows = new List<IndicatorRow>
        {
            new IndicatorRow("Current", CurrentMode, QgcColors.ColorBlue),
        };
        rows.AddRange(AvailableModes.Select(m =>
            new IndicatorRow("", m,
                string.Equals(m, CurrentMode, StringComparison.Ordinal)
                    ? QgcColors.ColorGreen : null)));
        IndicatorPagePainter.Draw(context, Bounds.Width, "Flight Mode", rows);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, 1 + (AvailableModes?.Count ?? 0), hasHeader: true);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var modes    = AvailableModes;
        var firstRow = 1; // skip "Current" row
        var rowIndex = (int)((e.GetPosition(this).Y
                       - IndicatorPagePainter.Gap
                       - (ScreenMetrics.SmallFontPointSize + 7)) // header height
                       / IndicatorPagePainter.RowH) - firstRow + 1;
        if (rowIndex >= 0 && rowIndex < modes.Count)
            ModeChangeRequested?.Invoke(this, modes[rowIndex]);
    }
}

// ────────────────────────────────────────────────────────────────
// 125. MessageIndicatorPage
//      QGC: Toolbar/MessageIndicator.qml (expanded)
// ────────────────────────────────────────────────────────────────

/// <summary>Vehicle message log detail page.</summary>
public sealed record VehicleMessage(string Text, Color Severity);

/// <summary>Message detail page — recent vehicle log messages.</summary>
public sealed class MessageIndicatorPage : Control
{
    public static readonly StyledProperty<IReadOnlyList<VehicleMessage>> MessagesProperty =
        AvaloniaProperty.Register<MessageIndicatorPage, IReadOnlyList<VehicleMessage>>(
            nameof(Messages), Array.Empty<VehicleMessage>());

    static MessageIndicatorPage()
    {
        AffectsRender<MessageIndicatorPage>(MessagesProperty);
        AffectsMeasure<MessageIndicatorPage>(MessagesProperty);
    }

    public IReadOnlyList<VehicleMessage> Messages
    {
        get => GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var rows = Messages.Select(m => new IndicatorRow("", m.Text, m.Severity)).ToArray();
        IndicatorPagePainter.Draw(context, Bounds.Width, "Messages", rows);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, Messages?.Count ?? 0, hasHeader: true);
}

// ────────────────────────────────────────────────────────────────
// 126. GimbalIndicatorPage
//      QGC: Toolbar/GimbalIndicator.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Gimbal detail page — pitch, yaw, roll, lock/retract status.</summary>
public sealed class GimbalIndicatorPage : Control
{
    public static readonly StyledProperty<double> GimbalPitchProperty =
        AvaloniaProperty.Register<GimbalIndicatorPage, double>(nameof(GimbalPitch), double.NaN);
    public static readonly StyledProperty<double> GimbalYawProperty =
        AvaloniaProperty.Register<GimbalIndicatorPage, double>(nameof(GimbalYaw), double.NaN);
    public static readonly StyledProperty<double> GimbalRollProperty =
        AvaloniaProperty.Register<GimbalIndicatorPage, double>(nameof(GimbalRoll), double.NaN);
    public static readonly StyledProperty<bool> IsRetractedProperty =
        AvaloniaProperty.Register<GimbalIndicatorPage, bool>(nameof(IsRetracted), false);
    public static readonly StyledProperty<bool> IsYawLockedProperty =
        AvaloniaProperty.Register<GimbalIndicatorPage, bool>(nameof(IsYawLocked), false);

    static GimbalIndicatorPage() =>
        AffectsRender<GimbalIndicatorPage>(GimbalPitchProperty, GimbalYawProperty,
            GimbalRollProperty, IsRetractedProperty, IsYawLockedProperty);

    public double GimbalPitch  { get => GetValue(GimbalPitchProperty);  set => SetValue(GimbalPitchProperty, value); }
    public double GimbalYaw    { get => GetValue(GimbalYawProperty);    set => SetValue(GimbalYawProperty, value); }
    public double GimbalRoll   { get => GetValue(GimbalRollProperty);   set => SetValue(GimbalRollProperty, value); }
    public bool   IsRetracted  { get => GetValue(IsRetractedProperty);  set => SetValue(IsRetractedProperty, value); }
    public bool   IsYawLocked  { get => GetValue(IsYawLockedProperty);  set => SetValue(IsYawLockedProperty, value); }

    private static string Ang(double v) =>
        double.IsNaN(v) ? "–" : $"{v:F1}°";

    public override void Render(DrawingContext context)
    {
        IndicatorPagePainter.Draw(context, Bounds.Width, "Gimbal",
        [
            new IndicatorRow("Pitch",     Ang(GimbalPitch)),
            new IndicatorRow("Yaw",       Ang(GimbalYaw)),
            new IndicatorRow("Roll",      Ang(GimbalRoll)),
            new IndicatorRow("Yaw Lock",  IsYawLocked  ? "Locked"   : "Free",
                             IsYawLocked  ? QgcColors.ColorBlue  : null),
            new IndicatorRow("State",     IsRetracted  ? "Retracted" : "Deployed",
                             IsRetracted  ? QgcColors.ColorOrange : QgcColors.ColorGreen),
        ]);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, 5, hasHeader: true);
}

// ────────────────────────────────────────────────────────────────
// 127. JoystickIndicatorPage
//      QGC: Toolbar/JoystickIndicator.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Joystick detail page — device name, calibration state, axis/button counts.</summary>
public sealed class JoystickIndicatorPage : Control
{
    public static readonly StyledProperty<string> DeviceNameProperty =
        AvaloniaProperty.Register<JoystickIndicatorPage, string>(nameof(DeviceName), "No joystick");
    public static readonly StyledProperty<bool>   IsCalibratedProperty =
        AvaloniaProperty.Register<JoystickIndicatorPage, bool>(nameof(IsCalibrated), false);
    public static readonly StyledProperty<bool>   JoystickEnabledProperty =
        AvaloniaProperty.Register<JoystickIndicatorPage, bool>(nameof(JoystickEnabled), false);
    public static readonly StyledProperty<int>    AxisCountProperty =
        AvaloniaProperty.Register<JoystickIndicatorPage, int>(nameof(AxisCount), 0);
    public static readonly StyledProperty<int>    ButtonCountProperty =
        AvaloniaProperty.Register<JoystickIndicatorPage, int>(nameof(ButtonCount), 0);

    static JoystickIndicatorPage() =>
        AffectsRender<JoystickIndicatorPage>(DeviceNameProperty, IsCalibratedProperty,
            JoystickEnabledProperty, AxisCountProperty, ButtonCountProperty);

    public string DeviceName  { get => GetValue(DeviceNameProperty);  set => SetValue(DeviceNameProperty, value); }
    public bool IsCalibrated  { get => GetValue(IsCalibratedProperty); set => SetValue(IsCalibratedProperty, value); }
    public bool JoystickEnabled  { get => GetValue(JoystickEnabledProperty); set => SetValue(JoystickEnabledProperty, value); }
    public int  AxisCount     { get => GetValue(AxisCountProperty);   set => SetValue(AxisCountProperty, value); }
    public int  ButtonCount   { get => GetValue(ButtonCountProperty); set => SetValue(ButtonCountProperty, value); }

    public override void Render(DrawingContext context)
    {
        IndicatorPagePainter.Draw(context, Bounds.Width, "Joystick",
        [
            new IndicatorRow("Device",      DeviceName),
            new IndicatorRow("Enabled",     JoystickEnabled ? "Yes"  : "No",
                             JoystickEnabled  ? QgcColors.ColorGreen  : QgcColors.ColorGrey),
            new IndicatorRow("Calibrated",  IsCalibrated  ? "Yes"  : "No",
                             IsCalibrated   ? QgcColors.ColorGreen  : QgcColors.ColorOrange),
            new IndicatorRow("Axes",        AxisCount.ToString()),
            new IndicatorRow("Buttons",     ButtonCount.ToString()),
        ]);
    }

    protected override Size MeasureOverride(Size s) =>
        IndicatorPagePainter.Measure(s.Width, 5, hasHeader: true);
}

// ────────────────────────────────────────────────────────────────
// 128. RemoteIDIndicatorPage
//      QGC: Toolbar/RemoteIDIndicatorPage.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Remote ID status detail page — flag grid + emergency declare button.</summary>
public sealed class RemoteIDIndicatorPage : Control
{
    public static readonly StyledProperty<bool> IsGpsGoodProperty =
        AvaloniaProperty.Register<RemoteIDIndicatorPage, bool>(nameof(IsGpsGood), false);
    public static readonly StyledProperty<bool> IsBasicIdGoodProperty =
        AvaloniaProperty.Register<RemoteIDIndicatorPage, bool>(nameof(IsBasicIdGood), false);
    public static readonly StyledProperty<bool> IsArmStatusGoodProperty =
        AvaloniaProperty.Register<RemoteIDIndicatorPage, bool>(nameof(IsArmStatusGood), false);
    public static readonly StyledProperty<bool> IsCommsGoodProperty =
        AvaloniaProperty.Register<RemoteIDIndicatorPage, bool>(nameof(IsCommsGood), false);
    public static readonly StyledProperty<bool> IsEmergencyDeclaredProperty =
        AvaloniaProperty.Register<RemoteIDIndicatorPage, bool>(nameof(IsEmergencyDeclared), false);

    static RemoteIDIndicatorPage() =>
        AffectsRender<RemoteIDIndicatorPage>(IsGpsGoodProperty, IsBasicIdGoodProperty,
            IsArmStatusGoodProperty, IsCommsGoodProperty, IsEmergencyDeclaredProperty);

    public bool IsGpsGood           { get => GetValue(IsGpsGoodProperty);           set => SetValue(IsGpsGoodProperty, value); }
    public bool IsBasicIdGood       { get => GetValue(IsBasicIdGoodProperty);       set => SetValue(IsBasicIdGoodProperty, value); }
    public bool IsArmStatusGood     { get => GetValue(IsArmStatusGoodProperty);     set => SetValue(IsArmStatusGoodProperty, value); }
    public bool IsCommsGood         { get => GetValue(IsCommsGoodProperty);         set => SetValue(IsCommsGoodProperty, value); }
    public bool IsEmergencyDeclared { get => GetValue(IsEmergencyDeclaredProperty); set => SetValue(IsEmergencyDeclaredProperty, value); }

    public event EventHandler? EmergencyToggleRequested;

    private const double FlagW = 80, FlagH = 44, FlagGap = 6, TopPad = 8;

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        // Draw 2×2 flag grid
        var (flags, labels) = (
            new[] { IsArmStatusGood, IsCommsGood, IsGpsGood, IsBasicIdGood },
            new[] { "ARM STATUS",   "RID COMMS", "GCS GPS",  "BASIC ID"    });

        var startX = (w - 2 * FlagW - FlagGap) / 2;
        var y = TopPad;
        var tf = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);

        for (var i = 0; i < 4; i++)
        {
            var col = i % 2;
            var row = i / 2;
            var fx  = startX + col * (FlagW + FlagGap);
            var fy  = y + row * (FlagH + FlagGap);
            var clr = flags[i] ? QgcColors.ColorGreen : QgcColors.ColorRed;
            context.DrawRectangle(new SolidColorBrush(clr), null,
                new Rect(fx, fy, FlagW, FlagH), 4, 4);

            var ft = new FormattedText(labels[i], CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(QgcColors.ButtonText));
            context.DrawText(ft,
                new Point(fx + (FlagW - ft.Width) / 2, fy + (FlagH - ft.Height) / 2));
        }

        // Emergency button
        var btnY = y + 2 * (FlagH + FlagGap) + 8;
        var btnClr = IsEmergencyDeclared ? QgcColors.ColorOrange : QgcColors.ColorRed;
        context.DrawRectangle(new SolidColorBrush(btnClr), null,
            new Rect(startX, btnY, 2 * FlagW + FlagGap, FlagH), 4, 4);
        var btnTf = new FormattedText(
            IsEmergencyDeclared ? "Clear Emergency" : "EMERGENCY",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf,
            ScreenMetrics.DefaultFontPointSize,
            new SolidColorBrush(QgcColors.ButtonText));
        context.DrawText(btnTf, new Point(
            startX + (2 * FlagW + FlagGap - btnTf.Width) / 2,
            btnY + (FlagH - btnTf.Height) / 2));
    }

    protected override Size MeasureOverride(Size s)
    {
        var w = double.IsInfinity(s.Width) ? 280 : s.Width;
        var h = TopPad + 2 * (FlagH + FlagGap) + 8 + FlagH + 8;
        return new Size(w, h);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var btnY = TopPad + 2 * (FlagH + FlagGap) + 8;
        if (e.GetPosition(this).Y >= btnY)
            EmergencyToggleRequested?.Invoke(this, EventArgs.Empty);
    }
}

// ────────────────────────────────────────────────────────────────
// 129. EscIndicatorPage
//      QGC: Toolbar/EscIndicatorPage.qml
// ────────────────────────────────────────────────────────────────

/// <summary>ESC / motor telemetry detail page — per-motor RPM, voltage, current, temp, errors.</summary>
public sealed class EscIndicatorPage : Control
{
    public static readonly StyledProperty<IReadOnlyList<EscEntry>> EscEntriesProperty =
        AvaloniaProperty.Register<EscIndicatorPage, IReadOnlyList<EscEntry>>(
            nameof(EscEntries), Array.Empty<EscEntry>());

    static EscIndicatorPage()
    {
        AffectsRender<EscIndicatorPage>(EscEntriesProperty);
        AffectsMeasure<EscIndicatorPage>(EscEntriesProperty);
    }

    public IReadOnlyList<EscEntry> EscEntries
    {
        get => GetValue(EscEntriesProperty);
        set => SetValue(EscEntriesProperty, value);
    }

    private const double BlockH = 80;
    private const double BlockPad = 6;

    public override void Render(DrawingContext context)
    {
        var w    = Bounds.Width;
        var tf   = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var tfB  = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var pen  = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);

        // Summary row
        var escs    = EscEntries;
        var healthy = escs.Count(e => e.IsOnline && e.ErrorCount == 0);
        var summary = new FormattedText(
            $"ESC Status   {healthy}/{escs.Count} healthy",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfB,
            ScreenMetrics.DefaultFontPointSize,
            new SolidColorBrush(healthy == escs.Count ? QgcColors.ColorGreen : QgcColors.ColorOrange));
        context.DrawText(summary, new Point(BlockPad, BlockPad));
        var y = BlockPad + summary.Height + 4;
        context.DrawLine(pen, new Point(BlockPad, y), new Point(w - BlockPad, y));
        y += 4;

        // Per-motor blocks
        foreach (var esc in escs)
        {
            var borderClr = (esc.IsOnline && esc.ErrorCount == 0)
                ? QgcColors.ColorGreen : QgcColors.ColorRed;
            context.DrawRectangle(null, new Pen(new SolidColorBrush(borderClr), 1),
                new Rect(BlockPad, y, w - 2 * BlockPad, BlockH), 4, 4);

            var hdr = new FormattedText(
                $"Motor {esc.Index + 1}{(esc.IsOnline ? "" : " — OFFLINE")}",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tfB,
                ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(esc.IsOnline ? QgcColors.Text : QgcColors.ColorRed));
            context.DrawText(hdr, new Point(BlockPad + 6, y + 4));

            var iy = y + 4 + hdr.Height + 2;
            var col1 = new[] {
                $"RPM:  {esc.Rpm:N0}",
                $"V:    {esc.Voltage:F2} V",
            };
            var col2 = new[] {
                $"A:    {esc.Current:F1} A",
                $"Temp: {esc.TemperatureCelsius:F1} °C",
                $"Err:  {esc.ErrorCount}",
            };

            var cx = BlockPad + 6;
            foreach (var line in col1)
            {
                var ft = new FormattedText(line, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, tf, ScreenMetrics.SmallFontPointSize,
                    new SolidColorBrush(QgcColors.Text));
                context.DrawText(ft, new Point(cx, iy));
                iy += ft.Height + 1;
            }

            iy = y + 4 + hdr.Height + 2;
            foreach (var line in col2)
            {
                var errBrush = line.StartsWith("Err:") && esc.ErrorCount > 0
                    ? new SolidColorBrush(QgcColors.ColorRed)
                    : new SolidColorBrush(QgcColors.Text);
                var ft = new FormattedText(line, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, tf, ScreenMetrics.SmallFontPointSize, errBrush);
                context.DrawText(ft, new Point(w / 2, iy));
                iy += ft.Height + 1;
            }

            y += BlockH + 4;
        }
    }

    protected override Size MeasureOverride(Size s)
    {
        var w       = double.IsInfinity(s.Width) ? 280 : s.Width;
        var count   = EscEntries?.Count ?? 0;
        var summary = BlockPad + ScreenMetrics.DefaultFontPointSize + 4 + 4;
        return new Size(w, summary + count * (BlockH + 4) + BlockPad);
    }
}

// ═══════════════════════════════════════════════════════════════
// RTK GPS Toolbar Indicator
// Equivalent to QGC Toolbar/RTKGPSIndicator.qml
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// RTK GPS toolbar indicator — shown when an RTK-capable GPS is connected but no vehicle is active.
/// Extends <see cref="ToolbarIndicatorBase"/> with RTK fix and survey-in state.
/// Equivalent to QGC Toolbar/RTKGPSIndicator.qml (extends GPSIndicator with RTK-specific visibility).
/// </summary>
public sealed class RtkGpsToolbarIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<bool> IsRtkFixedProperty =
        AvaloniaProperty.Register<RtkGpsToolbarIndicator, bool>(nameof(IsRtkFixed));

    public static readonly StyledProperty<bool> SurveyInProgressProperty =
        AvaloniaProperty.Register<RtkGpsToolbarIndicator, bool>(nameof(SurveyInProgress));

    public static readonly StyledProperty<double> SurveyAccuracyMetersProperty =
        AvaloniaProperty.Register<RtkGpsToolbarIndicator, double>(nameof(SurveyAccuracyMeters), -1.0);

    public static readonly StyledProperty<int> ObservationsProperty =
        AvaloniaProperty.Register<RtkGpsToolbarIndicator, int>(nameof(Observations));

    public bool   IsRtkFixed            { get => GetValue(IsRtkFixedProperty);            set => SetValue(IsRtkFixedProperty, value); }
    public bool   SurveyInProgress      { get => GetValue(SurveyInProgressProperty);      set => SetValue(SurveyInProgressProperty, value); }
    public double SurveyAccuracyMeters  { get => GetValue(SurveyAccuracyMetersProperty);  set => SetValue(SurveyAccuracyMetersProperty, value); }
    public int    Observations          { get => GetValue(ObservationsProperty);           set => SetValue(ObservationsProperty, value); }

    public RtkGpsToolbarIndicator()
    {
        Label = "RTK";
    }

    /// <summary>Status color (blue = RTK fixed, orange = float or survey-in).</summary>
    public Color GetStateColor() =>
        SurveyInProgress || !IsRtkFixed ? QgcColors.ColorOrange : QgcColors.ColorBlue;

    /// <summary>Short sub-label for use in control templates (e.g., "FIXED", "SIN 0.02m").</summary>
    public string GetSubLabel()
    {
        if (SurveyInProgress)
            return SurveyAccuracyMeters >= 0 ? $"{SurveyAccuracyMeters:F2}m" : $"{Observations} obs";
        return IsRtkFixed ? "FIXED" : "FLOAT";
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SubMenuButton  (#137 — QGC SubMenuButton: icon + label button in drop panels)
// Used in ToolStripDropPanel pop-outs and similar secondary menus.  Renders an
// icon (optional) above a short text label, highlighted when hovered/active.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SubMenuButton : Control
{
    public static readonly StyledProperty<string> SubMenuButtonTextProperty =
        AvaloniaProperty.Register<SubMenuButton, string>(nameof(ButtonText), string.Empty);

    public static readonly StyledProperty<Geometry?> IconGeometryProperty =
        AvaloniaProperty.Register<SubMenuButton, Geometry?>(nameof(IconGeometry));

    public static readonly StyledProperty<bool> SubMenuIsActiveProperty =
        AvaloniaProperty.Register<SubMenuButton, bool>(nameof(IsActive), false);

    static SubMenuButton()
    {
        AffectsRender<SubMenuButton>(SubMenuButtonTextProperty, IconGeometryProperty, SubMenuIsActiveProperty);
        FocusableProperty.OverrideMetadata<SubMenuButton>(new StyledPropertyMetadata<bool>(true));
    }

    public string    ButtonText   { get => GetValue(SubMenuButtonTextProperty);   set => SetValue(SubMenuButtonTextProperty, value); }
    public Geometry? IconGeometry { get => GetValue(IconGeometryProperty);        set => SetValue(IconGeometryProperty, value); }
    public bool      IsActive     { get => GetValue(SubMenuIsActiveProperty);     set => SetValue(SubMenuIsActiveProperty, value); }

    public event EventHandler? Clicked;

    private bool _hovered;

    public override void Render(DrawingContext ctx)
    {
        var dfw    = ScreenMetrics.DefaultFontPixelWidth;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);
        var w      = bounds.Width;
        var h      = bounds.Height;

        // Background
        if (IsActive)
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(76, QgcColors.ColorGreen.R, QgcColors.ColorGreen.G, QgcColors.ColorGreen.B)), null, bounds);
        else if (_hovered)
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(38, QgcColors.ColorGrey.R, QgcColors.ColorGrey.G, QgcColors.ColorGrey.B)), null, bounds);

        double iconH = h * 0.45;
        double cx    = w / 2.0;

        // Icon (if geometry provided)
        if (IconGeometry is { } geo)
        {
            var geoBounds = geo.Bounds;
            if (geoBounds.Width > 0 && geoBounds.Height > 0)
            {
                double scale  = Math.Min((w - dfw) / geoBounds.Width, (iconH - 4) / geoBounds.Height);
                double tx     = cx - geoBounds.Width * scale / 2.0 - geoBounds.X * scale;
                double ty     = 4 - geoBounds.Y * scale;
                var    matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(tx, ty);
                using var _ = ctx.PushTransform(matrix);
                ctx.DrawGeometry(new SolidColorBrush(QgcColors.Text), null, geo);
            }
        }

        // Text label
        if (!string.IsNullOrEmpty(ButtonText))
        {
            var ft = new FormattedText(
                ButtonText,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                dfh * 0.85,
                new SolidColorBrush(QgcColors.Text));
            double tx = cx - ft.Width / 2.0;
            double ty = iconH + 2;
            ctx.DrawText(ft, new Point(Math.Max(0, tx), ty));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = double.IsInfinity(availableSize.Width)  ? dfw * 8  : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? dfh * 4  : availableSize.Height;
        return new Size(w, h);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// QGCHoverButton  (#150 — circular icon button with hover highlight ring)
// A round button that contains a geometry icon.  On hover a translucent disc
// is drawn; when active (IsActive=true) the disc is drawn permanently.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class QGCHoverButton : Control
{
    public static readonly StyledProperty<Geometry?> HoverIconGeometryProperty =
        AvaloniaProperty.Register<QGCHoverButton, Geometry?>(nameof(IconGeometry));
    public static readonly StyledProperty<bool>      HoverIsActiveProperty =
        AvaloniaProperty.Register<QGCHoverButton, bool>(nameof(IsActive), false);
    public static readonly StyledProperty<Color>     HoverActiveColorProperty =
        AvaloniaProperty.Register<QGCHoverButton, Color>(nameof(ActiveColor), QgcColors.ColorBlue);
    public static readonly StyledProperty<double>    ButtonSizeProperty =
        AvaloniaProperty.Register<QGCHoverButton, double>(nameof(ButtonSize), 36.0);

    static QGCHoverButton()
    {
        AffectsRender<QGCHoverButton>(HoverIconGeometryProperty, HoverIsActiveProperty, HoverActiveColorProperty, ButtonSizeProperty);
        FocusableProperty.OverrideMetadata<QGCHoverButton>(new StyledPropertyMetadata<bool>(true));
    }

    public Geometry? IconGeometry { get => GetValue(HoverIconGeometryProperty); set => SetValue(HoverIconGeometryProperty, value); }
    public bool      IsActive     { get => GetValue(HoverIsActiveProperty);     set => SetValue(HoverIsActiveProperty, value); }
    public Color     ActiveColor  { get => GetValue(HoverActiveColorProperty);  set => SetValue(HoverActiveColorProperty, value); }
    public double    ButtonSize   { get => GetValue(ButtonSizeProperty);        set => SetValue(ButtonSizeProperty, value); }

    public event EventHandler? Clicked;

    private bool _hovered;

    public override void Render(DrawingContext ctx)
    {
        var sz = ButtonSize;
        var cx = Bounds.Width  / 2;
        var cy = Bounds.Height / 2;
        var r  = sz / 2.0 - 1;

        // Background disc
        if (IsActive)
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(76,
                ActiveColor.R, ActiveColor.G, ActiveColor.B)), null, new Point(cx, cy), r, r);
        else if (_hovered)
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)), null, new Point(cx, cy), r, r);

        // Ring border
        ctx.DrawEllipse(null, new Pen(new SolidColorBrush(IsActive ? ActiveColor : QgcColors.ColorGrey), 1.0),
            new Point(cx, cy), r, r);

        // Icon
        if (IconGeometry is { } geo)
        {
            var gb = geo.Bounds;
            if (gb.Width > 0 && gb.Height > 0)
            {
                double iconSz = sz * 0.5;
                double scale  = Math.Min(iconSz / gb.Width, iconSz / gb.Height);
                double tx     = cx - gb.Width  * scale / 2.0 - gb.X * scale;
                double ty     = cy - gb.Height * scale / 2.0 - gb.Y * scale;
                using var _ = ctx.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(tx, ty));
                ctx.DrawGeometry(new SolidColorBrush(IsActive ? ActiveColor : QgcColors.Text), null, geo);
            }
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e); _hovered = true; InvalidateVisual();
    }
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e); _hovered = false; InvalidateVisual();
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var sz = ButtonSize;
        return new Size(sz, sz);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// MultiVehicleOffScreenIndicator  (#151)
// When a vehicle is outside the visible map area this control draws a colored
// arrow on the map edge pointing toward that vehicle's bearing.
// BearingDegrees: 0 = up/North, CW positive.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MultiVehicleOffScreenIndicator : Control
{
    public static readonly StyledProperty<double> BearingDegreesProperty =
        AvaloniaProperty.Register<MultiVehicleOffScreenIndicator, double>(nameof(BearingDegrees), 0.0);
    public static readonly StyledProperty<Color>  VehicleColorProperty =
        AvaloniaProperty.Register<MultiVehicleOffScreenIndicator, Color>(nameof(VehicleColor), QgcColors.ColorBlue);
    public static readonly StyledProperty<int>    VehicleIdProperty =
        AvaloniaProperty.Register<MultiVehicleOffScreenIndicator, int>(nameof(VehicleId), 1);

    static MultiVehicleOffScreenIndicator()
    {
        AffectsRender<MultiVehicleOffScreenIndicator>(BearingDegreesProperty, VehicleColorProperty, VehicleIdProperty);
    }

    public double BearingDegrees { get => GetValue(BearingDegreesProperty); set => SetValue(BearingDegreesProperty, value); }
    public Color  VehicleColor   { get => GetValue(VehicleColorProperty);   set => SetValue(VehicleColorProperty, value); }
    public int    VehicleId      { get => GetValue(VehicleIdProperty);      set => SetValue(VehicleIdProperty, value); }

    public event EventHandler? OffScreenTapped;

    public override void Render(DrawingContext ctx)
    {
        var w    = Bounds.Width;
        var h    = Bounds.Height;
        var cx   = w / 2;
        var cy   = h / 2;
        var rad  = BearingDegrees * Math.PI / 180.0;
        var fill = new SolidColorBrush(VehicleColor);
        var pen  = new Pen(new SolidColorBrush(Colors.Black), 1.0);

        // Arrow triangle pointing in bearing direction, centered in control
        double aw = w * 0.35;  // arrow half-width
        double ah = h * 0.50;  // arrow length

        // Tip at bearing direction, base opposite
        double tipX  = cx + ah * Math.Sin(rad);
        double tipY  = cy - ah * Math.Cos(rad);
        double base1X = cx + aw * Math.Cos(rad);
        double base1Y = cy + aw * Math.Sin(rad);
        double base2X = cx - aw * Math.Cos(rad);
        double base2Y = cy - aw * Math.Sin(rad);

        var arrow = new StreamGeometry();
        using (var sgc = arrow.Open())
        {
            sgc.BeginFigure(new Point(tipX, tipY), true);
            sgc.LineTo(new Point(base1X, base1Y));
            sgc.LineTo(new Point(base2X, base2Y));
            sgc.EndFigure(true);
        }
        ctx.DrawGeometry(fill, pen, arrow);

        // Vehicle ID label
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var ft  = new FormattedText(VehicleId.ToString(), System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75, new SolidColorBrush(Colors.White));
        ctx.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        return new Size(dfw * 4, dfw * 4);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        OffScreenTapped?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FlightModeButton  (#161 — large tappable flight mode selector button)
// Renders as a pill-shaped button with the mode name.  When IsCurrentMode=true
// the pill fills with ModeButtonColor to indicate it is the active mode.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class FlightModeButton : Control
{
    public static readonly StyledProperty<string> ModeNameProperty =
        AvaloniaProperty.Register<FlightModeButton, string>(nameof(ModeName), "Mode");
    public static readonly StyledProperty<bool>   IsCurrentModeProperty =
        AvaloniaProperty.Register<FlightModeButton, bool>(nameof(IsCurrentMode), false);
    public static readonly StyledProperty<Color>  ModeButtonColorProperty =
        AvaloniaProperty.Register<FlightModeButton, Color>(nameof(ModeButtonColor), QgcColors.ColorBlue);
    public static readonly StyledProperty<bool>   ModeEnabledProperty =
        AvaloniaProperty.Register<FlightModeButton, bool>(nameof(ModeEnabled), true);

    static FlightModeButton()
    {
        AffectsRender<FlightModeButton>(ModeNameProperty, IsCurrentModeProperty, ModeButtonColorProperty, ModeEnabledProperty);
        FocusableProperty.OverrideMetadata<FlightModeButton>(new StyledPropertyMetadata<bool>(true));
    }

    public string ModeName        { get => GetValue(ModeNameProperty);        set => SetValue(ModeNameProperty, value); }
    public bool   IsCurrentMode   { get => GetValue(IsCurrentModeProperty);   set => SetValue(IsCurrentModeProperty, value); }
    public Color  ModeButtonColor { get => GetValue(ModeButtonColorProperty); set => SetValue(ModeButtonColorProperty, value); }
    public bool   ModeEnabled     { get => GetValue(ModeEnabledProperty);     set => SetValue(ModeEnabledProperty, value); }

    public event EventHandler? ModeChangeRequested;

    private bool _hovered;

    public override void Render(DrawingContext ctx)
    {
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);
        var r      = bounds.Height / 2.0;

        // Pill background
        Color bgColor;
        if (!ModeEnabled)
            bgColor = QgcColors.WindowShade;
        else if (IsCurrentMode)
            bgColor = ModeButtonColor;
        else if (_hovered)
            bgColor = Color.FromArgb(80, ModeButtonColor.R, ModeButtonColor.G, ModeButtonColor.B);
        else
            bgColor = QgcColors.Button;

        ctx.DrawRectangle(new SolidColorBrush(bgColor), null, bounds, r, r);

        // Border
        ctx.DrawRectangle(null,
            new Pen(new SolidColorBrush(IsCurrentMode ? ModeButtonColor : QgcColors.Button), 1.5),
            bounds.Deflate(0.75), r, r);

        // Mode name text
        var textColor = !ModeEnabled ? QgcColors.DisabledText :
                        IsCurrentMode ? QgcColors.ButtonHighlightText : QgcColors.ButtonText;
        var ft = new FormattedText(ModeName, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
            new SolidColorBrush(textColor));
        ctx.DrawText(ft, new Point((bounds.Width - ft.Width) / 2, (bounds.Height - ft.Height) / 2));
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e); _hovered = true; InvalidateVisual();
    }
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e); _hovered = false; InvalidateVisual();
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ModeEnabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ModeChangeRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        var ft  = new FormattedText(ModeName, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9, new SolidColorBrush(QgcColors.ButtonText));
        double w = ft.Width + dfw * 2;
        return new Size(Math.Max(w, ScreenMetrics.ImplicitButtonWidth), dfh * 1.6);
    }
}

// ── #174 LinkStatusIndicator ──────────────────────────────────────────────────
public class LinkStatusIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<int>  LinkSignalQualityProperty =
        AvaloniaProperty.Register<LinkStatusIndicator, int>("LinkSignalQuality", 0);
    public static readonly StyledProperty<int>  LatencyMsProperty =
        AvaloniaProperty.Register<LinkStatusIndicator, int>("LatencyMs", -1);
    public static readonly StyledProperty<bool> IsLinkConnectedProperty =
        AvaloniaProperty.Register<LinkStatusIndicator, bool>("IsLinkConnected", false);

    public int  LinkSignalQuality { get => GetValue(LinkSignalQualityProperty); set => SetValue(LinkSignalQualityProperty, value); }
    public int  LatencyMs         { get => GetValue(LatencyMsProperty);         set => SetValue(LatencyMsProperty, value); }
    public bool IsLinkConnected   { get => GetValue(IsLinkConnectedProperty);   set => SetValue(IsLinkConnectedProperty, value); }

    static LinkStatusIndicator()
    {
        AffectsRender<LinkStatusIndicator>(LinkSignalQualityProperty, LatencyMsProperty, IsLinkConnectedProperty);
    }

    public LinkStatusIndicator() { Label = "LINK"; }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        bool connected = IsLinkConnected;
        int  quality   = LinkSignalQuality;

        // 4 ascending vertical bars
        int    barCount  = 4;
        double barW      = w * 0.12;
        double gap       = barW * 0.5;
        double totalBW   = barCount * barW + (barCount - 1) * gap;
        double startX    = (w - totalBW) / 2;
        double maxBarH   = h * 0.5;
        double baseY     = h * 0.55;

        for (int i = 0; i < barCount; i++)
        {
            double threshold = (i + 1) * 25; // 25 / 50 / 75 / 100
            bool   filled    = connected && quality >= threshold;
            double barH      = maxBarH * (i + 1) / barCount;
            double x         = startX + i * (barW + gap);
            double y         = baseY - barH;

            var brush = filled
                ? new SolidColorBrush(quality > 50 ? QgcColors.ColorGreen : QgcColors.ColorOrange)
                : new SolidColorBrush(QgcColors.ColorGrey);
            dc.FillRectangle(brush, new Rect(x, y, barW, barH));
        }

        // sub-label: latency or "LOST"
        string sub = connected
            ? (LatencyMs >= 0 ? $"{LatencyMs}ms" : "---")
            : "LOST";
        var subBrush = connected ? new SolidColorBrush(QgcColors.Text) : new SolidColorBrush(QgcColors.ColorRed);
        var ft = new FormattedText(sub, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.65, subBrush);
        dc.DrawText(ft, new Point((w - ft.Width) / 2, baseY + 2));
    }
}

// ── #175 VideoStreamIndicator ─────────────────────────────────────────────────
public class VideoStreamIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<bool>   IsVideoStreamingProperty =
        AvaloniaProperty.Register<VideoStreamIndicator, bool>("IsVideoStreaming", false);
    public static readonly StyledProperty<string> StreamResolutionStrProperty =
        AvaloniaProperty.Register<VideoStreamIndicator, string>("StreamResolutionStr", string.Empty);
    public static readonly StyledProperty<int>    StreamBitrateKbpsProperty =
        AvaloniaProperty.Register<VideoStreamIndicator, int>("StreamBitrateKbps", 0);

    public bool   IsVideoStreaming    { get => GetValue(IsVideoStreamingProperty);    set => SetValue(IsVideoStreamingProperty, value); }
    public string StreamResolutionStr { get => GetValue(StreamResolutionStrProperty); set => SetValue(StreamResolutionStrProperty, value); }
    public int    StreamBitrateKbps   { get => GetValue(StreamBitrateKbpsProperty);   set => SetValue(StreamBitrateKbpsProperty, value); }

    static VideoStreamIndicator()
    {
        AffectsRender<VideoStreamIndicator>(IsVideoStreamingProperty, StreamResolutionStrProperty, StreamBitrateKbpsProperty);
    }

    public VideoStreamIndicator() { Label = "VID"; }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        bool streaming = IsVideoStreaming;

        // ON / OFF pill
        string pillText  = streaming ? "ON" : "OFF";
        Color  pillColor = streaming ? QgcColors.ColorGreen : QgcColors.ColorGrey;
        double pillH     = dfh * 0.9;
        double pillW     = dfh * 1.6;
        double pillX     = (w - pillW) / 2;
        double pillY     = h * 0.18;
        var pillBrush    = new SolidColorBrush(pillColor);
        dc.DrawRectangle(pillBrush, null, new Rect(pillX, pillY, pillW, pillH), pillH / 2);

        var pillFt = new FormattedText(pillText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.65, new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(pillFt, new Point(pillX + (pillW - pillFt.Width) / 2, pillY + (pillH - pillFt.Height) / 2));

        // resolution sub-label
        if (streaming && !string.IsNullOrEmpty(StreamResolutionStr))
        {
            var resFt = new FormattedText(StreamResolutionStr, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.6, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(resFt, new Point((w - resFt.Width) / 2, pillY + pillH + 2));
        }
    }
}

// ── #176 EscStatusRow ─────────────────────────────────────────────────────────
public class EscStatusRow : Control
{
    public static readonly StyledProperty<int>    EscMotorIdProperty  =
        AvaloniaProperty.Register<EscStatusRow, int>("EscMotorId", 0);
    public static readonly StyledProperty<int>    EscRpmProperty      =
        AvaloniaProperty.Register<EscStatusRow, int>("EscRpm", 0);
    public static readonly StyledProperty<double> EscCurrentAProperty =
        AvaloniaProperty.Register<EscStatusRow, double>("EscCurrentA", 0.0);
    public static readonly StyledProperty<double> EscTempCProperty    =
        AvaloniaProperty.Register<EscStatusRow, double>("EscTempC", 0.0);
    public static readonly StyledProperty<double> EscVoltageVProperty =
        AvaloniaProperty.Register<EscStatusRow, double>("EscVoltageV", 0.0);
    public static readonly StyledProperty<bool>   EscIsActiveProperty =
        AvaloniaProperty.Register<EscStatusRow, bool>("EscIsActive", false);

    public int    EscMotorId  { get => GetValue(EscMotorIdProperty);  set => SetValue(EscMotorIdProperty, value); }
    public int    EscRpm      { get => GetValue(EscRpmProperty);      set => SetValue(EscRpmProperty, value); }
    public double EscCurrentA { get => GetValue(EscCurrentAProperty); set => SetValue(EscCurrentAProperty, value); }
    public double EscTempC    { get => GetValue(EscTempCProperty);    set => SetValue(EscTempCProperty, value); }
    public double EscVoltageV { get => GetValue(EscVoltageVProperty); set => SetValue(EscVoltageVProperty, value); }
    public bool   EscIsActive { get => GetValue(EscIsActiveProperty); set => SetValue(EscIsActiveProperty, value); }

    static EscStatusRow()
    {
        AffectsRender<EscStatusRow>(EscMotorIdProperty, EscRpmProperty, EscCurrentAProperty,
            EscTempCProperty, EscVoltageVProperty, EscIsActiveProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // alternating row background
        Color rowBg = (EscMotorId % 2 == 0)
            ? QgcColors.Window
            : Color.FromArgb(20, 255, 255, 255);
        dc.FillRectangle(new SolidColorBrush(rowBg), new Rect(0, 0, w, h));

        // active indicator dot
        bool active = EscIsActive;
        var dotBrush = new SolidColorBrush(active ? QgcColors.ColorGreen : QgcColors.ColorGrey);
        double dotR  = h * 0.25;
        double dotX  = h * 0.5;
        double dotY  = h * 0.5;
        dc.DrawEllipse(dotBrush, null, new Point(dotX, dotY), dotR, dotR);

        // motor ID
        var idFt = new FormattedText($"M{EscMotorId + 1}", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(idFt, new Point(h * 1.2, (h - idFt.Height) / 2));

        // 4 value columns: RPM / Current / Voltage / Temp
        double colW    = (w - h * 2.5) / 4;
        double colBase = h * 2.5;

        string[] labels = { $"{EscRpm}", $"{EscCurrentA:F1}A", $"{EscVoltageV:F1}V", $"{EscTempC:F0}°" };
        Color[]  colors = {
            QgcColors.Text,
            EscCurrentA > 40 ? QgcColors.ColorRed : EscCurrentA > 25 ? QgcColors.ColorOrange : QgcColors.Text,
            EscVoltageV < 3.5 ? QgcColors.ColorRed : QgcColors.Text,
            EscTempC > 80 ? QgcColors.ColorRed : EscTempC > 60 ? QgcColors.ColorOrange : QgcColors.Text
        };

        for (int i = 0; i < 4; i++)
        {
            var ft = new FormattedText(labels[i], System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75, new SolidColorBrush(colors[i]));
            double cx = colBase + i * colW + (colW - ft.Width) / 2;
            dc.DrawText(ft, new Point(cx, (h - ft.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(!double.IsInfinity(availableSize.Width) ? availableSize.Width : 320,
                        ScreenMetrics.DefaultFontPixelHeight * 1.6);
    }
}

// ── #184 HeadingIndicator ─────────────────────────────────────────────────────
public class HeadingIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<double> HeadingDegreesProperty =
        AvaloniaProperty.Register<HeadingIndicator, double>("HeadingDegrees", 0.0);
    public static readonly StyledProperty<bool>   IsHeadingValidProperty =
        AvaloniaProperty.Register<HeadingIndicator, bool>("IsHeadingValid", false);

    public double HeadingDegrees { get => GetValue(HeadingDegreesProperty); set => SetValue(HeadingDegreesProperty, value); }
    public bool   IsHeadingValid { get => GetValue(IsHeadingValidProperty); set => SetValue(IsHeadingValidProperty, value); }

    static HeadingIndicator()
    {
        AffectsRender<HeadingIndicator>(HeadingDegreesProperty, IsHeadingValidProperty);
    }

    public HeadingIndicator() { Label = "HDG"; }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        if (!IsHeadingValid)
        {
            var naFt = new FormattedText("---°", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(naFt, new Point((w - naFt.Width) / 2, h * 0.15));
            return;
        }

        // Heading value text
        string hdgStr = $"{(int)Math.Round(HeadingDegrees) % 360:D3}°";
        var hdgFt = new FormattedText(hdgStr, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.95,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(hdgFt, new Point((w - hdgFt.Width) / 2, h * 0.10));

        // Mini compass arc (semi-circle)
        double arcR   = Math.Min(w, h * 0.45) * 0.45;
        double cx     = w / 2;
        double cy     = h * 0.70;
        var arcPen    = new Pen(new SolidColorBrush(QgcColors.ColorGrey), 1.2);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(cx - arcR, cy), false);
            ctx.ArcTo(new Point(cx + arcR, cy), new Size(arcR, arcR), 0, false, SweepDirection.Clockwise);
        }
        dc.DrawGeometry(null, arcPen, geo);

        // North tick at heading angle
        double rad   = (HeadingDegrees - 90) * Math.PI / 180.0;
        double tickX = cx + Math.Cos(rad) * arcR;
        double tickY = cy + Math.Sin(rad) * arcR;
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.ColorGreen), 2),
            new Point(cx, cy), new Point(tickX, tickY));
    }
}

// ── #185 BatteryStatusIndicator ───────────────────────────────────────────────
public class BatteryStatusIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<double> BatteryVoltageProperty =
        AvaloniaProperty.Register<BatteryStatusIndicator, double>("BatteryVoltage", 0.0);
    public static readonly StyledProperty<int>    BatteryPercentProperty =
        AvaloniaProperty.Register<BatteryStatusIndicator, int>("BatteryPercent", -1);
    public static readonly StyledProperty<int>    BatteryCellCountProperty =
        AvaloniaProperty.Register<BatteryStatusIndicator, int>("BatteryCellCount", 1);

    public double BatteryVoltage    { get => GetValue(BatteryVoltageProperty);    set => SetValue(BatteryVoltageProperty, value); }
    public int    BatteryPercent    { get => GetValue(BatteryPercentProperty);    set => SetValue(BatteryPercentProperty, value); }
    public int    BatteryCellCount  { get => GetValue(BatteryCellCountProperty);  set => SetValue(BatteryCellCountProperty, value); }

    static BatteryStatusIndicator()
    {
        AffectsRender<BatteryStatusIndicator>(BatteryVoltageProperty, BatteryPercentProperty, BatteryCellCountProperty);
    }

    public BatteryStatusIndicator() { Label = "BAT"; }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        int    pct   = BatteryPercent;
        double volts = BatteryVoltage;
        bool   valid = pct >= 0;

        // Battery outline
        double battW = w * 0.7;
        double battH = h * 0.28;
        double battX = (w - battW) / 2;
        double battY = h * 0.10;
        double tipW  = battW * 0.06;
        double tipH  = battH * 0.4;
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(QgcColors.ColorGrey), 1.2),
            new Rect(battX, battY, battW, battH), 2);
        // tip
        dc.FillRectangle(new SolidColorBrush(QgcColors.ColorGrey),
            new Rect(battX + battW, battY + (battH - tipH) / 2, tipW, tipH));

        // Fill
        if (valid)
        {
            double fillW = (battW - 2) * Math.Clamp(pct / 100.0, 0, 1);
            Color fillColor = pct > 50 ? QgcColors.ColorGreen
                            : pct > 20 ? QgcColors.ColorOrange
                                       : QgcColors.ColorRed;
            dc.FillRectangle(new SolidColorBrush(fillColor),
                new Rect(battX + 1, battY + 1, fillW, battH - 2));
        }

        // Percentage
        string pctStr = valid ? $"{pct}%" : "---%";
        var pctFt = new FormattedText(pctStr, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(valid && pct <= 20 ? QgcColors.ColorRed : QgcColors.Text));
        dc.DrawText(pctFt, new Point((w - pctFt.Width) / 2, battY + battH + 2));

        // Voltage sub-label
        if (volts > 0)
        {
            string vStr = BatteryCellCount > 1
                ? $"{volts:F1}V ({volts / BatteryCellCount:F2}/cell)"
                : $"{volts:F2}V";
            var vFt = new FormattedText(vStr, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.62,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(vFt, new Point((w - vFt.Width) / 2, battY + battH + dfh * 0.95));
        }
    }
}

// ── #186 QGCHamburgerMenu ─────────────────────────────────────────────────────
public class QGCHamburgerMenu : ToolbarIndicatorBase
{
    public static readonly StyledProperty<bool> HamburgerIsOpenProperty =
        AvaloniaProperty.Register<QGCHamburgerMenu, bool>("HamburgerIsOpen", false);

    public bool HamburgerIsOpen { get => GetValue(HamburgerIsOpenProperty); set => SetValue(HamburgerIsOpenProperty, value); }

    public event EventHandler? MenuToggleRequested;

    static QGCHamburgerMenu()
    {
        AffectsRender<QGCHamburgerMenu>(HamburgerIsOpenProperty);
    }

    public QGCHamburgerMenu() { Label = string.Empty; }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        bool open     = HamburgerIsOpen;
        Color lineCol = open ? QgcColors.PrimaryButtonFill : QgcColors.Text;
        var linePen   = new Pen(new SolidColorBrush(lineCol), 2);
        double lineW  = w * 0.55;
        double startX = (w - lineW) / 2;
        double midY   = h / 2;
        double spacing= dfh * 0.4;

        dc.DrawLine(linePen, new Point(startX, midY - spacing), new Point(startX + lineW, midY - spacing));
        dc.DrawLine(linePen, new Point(startX, midY),           new Point(startX + lineW, midY));
        dc.DrawLine(linePen, new Point(startX, midY + spacing), new Point(startX + lineW, midY + spacing));

        // Dot indicator when open
        if (open)
        {
            dc.DrawEllipse(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
                new Point(w - 5, 5), 3, 3);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            MenuToggleRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}

// ── #194 InstrumentValueTile ──────────────────────────────────────────────────
public class InstrumentValueTile : Control
{
    public static readonly StyledProperty<string> IVTValueTextProperty =
        AvaloniaProperty.Register<InstrumentValueTile, string>("IVTValueText", "---");
    public static readonly StyledProperty<string> IVTLabelProperty =
        AvaloniaProperty.Register<InstrumentValueTile, string>("IVTLabel", string.Empty);
    public static readonly StyledProperty<string> IVTUnitProperty =
        AvaloniaProperty.Register<InstrumentValueTile, string>("IVTUnit", string.Empty);
    public static readonly StyledProperty<Color>  IVTValueColorProperty =
        AvaloniaProperty.Register<InstrumentValueTile, Color>("IVTValueColor", Colors.White);

    public string IVTValueText  { get => GetValue(IVTValueTextProperty);  set => SetValue(IVTValueTextProperty, value); }
    public string IVTLabel      { get => GetValue(IVTLabelProperty);      set => SetValue(IVTLabelProperty, value); }
    public string IVTUnit       { get => GetValue(IVTUnitProperty);       set => SetValue(IVTUnitProperty, value); }
    public Color  IVTValueColor { get => GetValue(IVTValueColorProperty); set => SetValue(IVTValueColorProperty, value); }

    static InstrumentValueTile()
    {
        AffectsRender<InstrumentValueTile>(IVTValueTextProperty, IVTLabelProperty, IVTUnitProperty, IVTValueColorProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        // Tile background
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder)), new Rect(0, 0, w, h), br);

        // Value (large, centered)
        var valFt = new FormattedText(IVTValueText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.Bold),
            dfh * 1.2, new SolidColorBrush(IVTValueColor));
        double valY = h * 0.18;
        dc.DrawText(valFt, new Point((w - valFt.Width) / 2, valY));

        // Unit (small, right of value on same baseline)
        if (!string.IsNullOrEmpty(IVTUnit))
        {
            var unitFt = new FormattedText(IVTUnit, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(unitFt, new Point((w + valFt.Width) / 2 + 2, valY + valFt.Height - unitFt.Height));
        }

        // Label (small, bottom center)
        if (!string.IsNullOrEmpty(IVTLabel))
        {
            var lblFt = new FormattedText(IVTLabel, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(lblFt, new Point((w - lblFt.Width) / 2, h - lblFt.Height - 4));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        return new Size(!double.IsInfinity(availableSize.Width) ? availableSize.Width : dfh * 6,
                        dfh * 3.5);
    }
}

// ── #203 MultiVehicleSelector ─────────────────────────────────────────────────
public class MultiVehicleSelector : Control
{
    public static readonly StyledProperty<IReadOnlyList<int>?> MVSVehicleIdsProperty =
        AvaloniaProperty.Register<MultiVehicleSelector, IReadOnlyList<int>?>("MVSVehicleIds");
    public static readonly StyledProperty<int> MVSSelectedIdProperty =
        AvaloniaProperty.Register<MultiVehicleSelector, int>("MVSSelectedId", 0);

    public IReadOnlyList<int>? MVSVehicleIds { get => GetValue(MVSVehicleIdsProperty); set => SetValue(MVSVehicleIdsProperty, value); }
    public int                 MVSSelectedId  { get => GetValue(MVSSelectedIdProperty); set => SetValue(MVSSelectedIdProperty, value); }

    public event EventHandler<int>? VehicleSelected;

    static MultiVehicleSelector()
    {
        AffectsRender<MultiVehicleSelector>(MVSVehicleIdsProperty, MVSSelectedIdProperty);
    }

    private readonly System.Collections.Generic.List<Rect> _pillRects = new();
    private readonly System.Collections.Generic.List<int>  _pillIds   = new();

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        _pillRects.Clear();
        _pillIds.Clear();

        var ids = MVSVehicleIds;
        if (ids == null || ids.Count == 0)
        {
            var emptyFt = new FormattedText("No vehicles", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(emptyFt, new Point((w - emptyFt.Width) / 2, (h - emptyFt.Height) / 2));
            return;
        }

        double pad    = 4;
        double pillH  = h - pad * 2;
        double x      = pad;

        foreach (int id in ids)
        {
            string label  = $"V{id}";
            var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
                new SolidColorBrush(id == MVSSelectedId ? QgcColors.ButtonText : QgcColors.Text));
            double pillW = ft.Width + dfh;
            bool   active = id == MVSSelectedId;

            var pillRect = new Rect(x, pad, pillW, pillH);
            _pillRects.Add(pillRect);
            _pillIds.Add(id);

            dc.DrawRectangle(
                new SolidColorBrush(active ? QgcColors.PrimaryButtonFill : QgcColors.Window),
                new Pen(new SolidColorBrush(QgcColors.GroupBorder)),
                pillRect, br);
            dc.DrawText(ft, new Point(x + (pillW - ft.Width) / 2, pad + (pillH - ft.Height) / 2));
            x += pillW + pad;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        for (int i = 0; i < _pillRects.Count; i++)
        {
            if (_pillRects[i].Contains(pos))
            {
                MVSSelectedId = _pillIds[i];
                VehicleSelected?.Invoke(this, _pillIds[i]);
                e.Handled = true;
                break;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        int count = MVSVehicleIds?.Count ?? 0;
        double w = count > 0 ? count * dfh * 4 : dfh * 6;
        return new Size(!double.IsInfinity(availableSize.Width) ? availableSize.Width : w,
                        dfh * 1.8);
    }
}

// ── #204 ArmTimeoutBar ────────────────────────────────────────────────────────
public class ArmTimeoutBar : Control
{
    public static readonly StyledProperty<double> ATBSecondsRemainingProperty =
        AvaloniaProperty.Register<ArmTimeoutBar, double>("ATBSecondsRemaining", 0.0);
    public static readonly StyledProperty<double> ATBTotalSecondsProperty =
        AvaloniaProperty.Register<ArmTimeoutBar, double>("ATBTotalSeconds", 30.0);
    public static readonly StyledProperty<bool>   ATBIsActiveProperty =
        AvaloniaProperty.Register<ArmTimeoutBar, bool>("ATBIsActive", false);

    public double ATBSecondsRemaining { get => GetValue(ATBSecondsRemainingProperty); set => SetValue(ATBSecondsRemainingProperty, value); }
    public double ATBTotalSeconds     { get => GetValue(ATBTotalSecondsProperty);     set => SetValue(ATBTotalSecondsProperty, value); }
    public bool   ATBIsActive         { get => GetValue(ATBIsActiveProperty);         set => SetValue(ATBIsActiveProperty, value); }

    static ArmTimeoutBar()
    {
        AffectsRender<ArmTimeoutBar>(ATBSecondsRemainingProperty, ATBTotalSecondsProperty, ATBIsActiveProperty);
    }

    public override void Render(DrawingContext dc)
    {
        if (!ATBIsActive) return;
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        double total = ATBTotalSeconds > 0 ? ATBTotalSeconds : 30;
        double rem   = Math.Clamp(ATBSecondsRemaining, 0, total);
        double ratio = rem / total;

        // Track
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
            new Rect(0, 0, w, h), br);

        // Fill (from left, shrinks as time runs out)
        Color fillColor = ratio > 0.5 ? QgcColors.ColorGreen
                        : ratio > 0.2 ? QgcColors.ColorOrange
                                      : QgcColors.ColorRed;
        double fillW = w * ratio;
        if (fillW > 0)
            dc.DrawRectangle(new SolidColorBrush(fillColor), null,
                new Rect(0, 0, fillW, h), br);

        // Countdown text
        string label = $"Arm timeout: {rem:F0}s";
        var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8,
            new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!ATBIsActive) return new Size(0, 0);
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 200;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 1.4);
    }
}

// ── #205 WindIndicator ────────────────────────────────────────────────────────
public class WindIndicator : ToolbarIndicatorBase
{
    public static readonly StyledProperty<double> WindSpeedMsProperty =
        AvaloniaProperty.Register<WindIndicator, double>("WindSpeedMs", 0.0);
    public static readonly StyledProperty<double> WindDirectionDegProperty =
        AvaloniaProperty.Register<WindIndicator, double>("WindDirectionDeg", 0.0);
    public static readonly StyledProperty<bool>   IsWindValidProperty =
        AvaloniaProperty.Register<WindIndicator, bool>("IsWindValid", false);

    public double WindSpeedMs      { get => GetValue(WindSpeedMsProperty);      set => SetValue(WindSpeedMsProperty, value); }
    public double WindDirectionDeg { get => GetValue(WindDirectionDegProperty); set => SetValue(WindDirectionDegProperty, value); }
    public bool   IsWindValid      { get => GetValue(IsWindValidProperty);      set => SetValue(IsWindValidProperty, value); }

    static WindIndicator()
    {
        AffectsRender<WindIndicator>(WindSpeedMsProperty, WindDirectionDegProperty, IsWindValidProperty);
    }

    public WindIndicator() { Label = "WIND"; }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        if (!IsWindValid)
        {
            var naFt = new FormattedText("---", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(naFt, new Point((w - naFt.Width) / 2, h * 0.15));
            return;
        }

        // Speed text
        var speedFt = new FormattedText($"{WindSpeedMs:F1}m/s", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.82,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(speedFt, new Point((w - speedFt.Width) / 2, h * 0.10));

        // Direction arrow
        double cx  = w / 2;
        double cy  = h * 0.65;
        double arR = Math.Min(w, h * 0.45) * 0.40;
        double rad = (WindDirectionDeg - 90) * Math.PI / 180.0;
        double tipX = cx + Math.Cos(rad) * arR;
        double tipY = cy + Math.Sin(rad) * arR;
        double baseX = cx - Math.Cos(rad) * arR * 0.6;
        double baseY = cy - Math.Sin(rad) * arR * 0.6;

        // Arrow shaft
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.ColorBlue), 2),
            new Point(baseX, baseY), new Point(tipX, tipY));

        // Arrow head
        double perpRad = rad + Math.PI / 2;
        double headSz  = arR * 0.4;
        var arrowGeo = new StreamGeometry();
        using (var ctx = arrowGeo.Open())
        {
            ctx.BeginFigure(new Point(tipX, tipY), true);
            ctx.LineTo(new Point(tipX - Math.Cos(rad) * headSz + Math.Cos(perpRad) * headSz * 0.5,
                                 tipY - Math.Sin(rad) * headSz + Math.Sin(perpRad) * headSz * 0.5));
            ctx.LineTo(new Point(tipX - Math.Cos(rad) * headSz - Math.Cos(perpRad) * headSz * 0.5,
                                 tipY - Math.Sin(rad) * headSz - Math.Sin(perpRad) * headSz * 0.5));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(new SolidColorBrush(QgcColors.ColorBlue), null, arrowGeo);
    }
}

// ── #214 SpeedIndicator ───────────────────────────────────────────────────────
// Displays airspeed (top) and groundspeed (bottom) as labelled numeric rows.
// SI prefix: "GS" / "AS" labels, value in m/s, colour-coded when airspeed is
// below stall threshold (SIStallSpeed).
public sealed class SpeedIndicator : Control
{
    public static readonly StyledProperty<double> SIAirspeedProperty =
        AvaloniaProperty.Register<SpeedIndicator, double>("SIAirspeed", double.NaN);
    public static readonly StyledProperty<double> SIGroundspeedProperty =
        AvaloniaProperty.Register<SpeedIndicator, double>("SIGroundspeed", double.NaN);
    public static readonly StyledProperty<double> SIStallSpeedProperty =
        AvaloniaProperty.Register<SpeedIndicator, double>("SIStallSpeed", 0.0);
    public static readonly StyledProperty<bool>   SIShowAirspeedProperty =
        AvaloniaProperty.Register<SpeedIndicator, bool>("SIShowAirspeed", true);

    static SpeedIndicator()
    {
        AffectsRender<SpeedIndicator>(SIAirspeedProperty, SIGroundspeedProperty,
                                      SIStallSpeedProperty, SIShowAirspeedProperty);
    }

    public double SIAirspeed    { get => GetValue(SIAirspeedProperty);    set => SetValue(SIAirspeedProperty, value); }
    public double SIGroundspeed { get => GetValue(SIGroundspeedProperty); set => SetValue(SIGroundspeedProperty, value); }
    public double SIStallSpeed  { get => GetValue(SIStallSpeedProperty);  set => SetValue(SIStallSpeedProperty, value); }
    public bool   SIShowAirspeed{ get => GetValue(SIShowAirspeedProperty);set => SetValue(SIShowAirspeedProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;
        double rowH = h / (SIShowAirspeed ? 2 : 1);

        void DrawRow(string label, double val, bool isAirspeed, double y)
        {
            bool stall = isAirspeed && SIStallSpeed > 0 && !double.IsNaN(val) && val < SIStallSpeed;
            Color valColor = stall ? QgcColors.ColorRed : QgcColors.Text;
            string valTxt  = double.IsNaN(val) ? "—" : $"{val:F1}";

            var lbFt = new FormattedText(label,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.7, new SolidColorBrush(QgcColors.TextSecondary));
            var vlFt = new FormattedText(valTxt,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.9, new SolidColorBrush(valColor));

            double midY = y + (rowH - dfh) / 2;
            dc.DrawText(lbFt, new Point(dfw * 0.5, midY + (dfh - lbFt.Height) / 2));
            dc.DrawText(vlFt, new Point(w - vlFt.Width - dfw * 0.5, midY + (dfh - vlFt.Height) / 2));
        }

        if (SIShowAirspeed)
            DrawRow("AS", SIAirspeed, true, 0);
        DrawRow("GS", SIGroundspeed, false, SIShowAirspeed ? rowH : 0);
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 90 : available.Width;
        double h = double.IsInfinity(available.Height) ? (SIShowAirspeed ? 40 : 24) : available.Height;
        return new Size(w, h);
    }
}

// ── #215 AltitudeIndicator ────────────────────────────────────────────────────
// Shows altitude value (large) with a small climb-rate indicator arrow.
// AIAltitude in metres, AIClimbRate in m/s (positive = up).
// Arrow direction and colour: green↑ (positive), red↓ (negative), grey = near zero.
public sealed class AltitudeIndicator : Control
{
    public static readonly StyledProperty<double> AIAltitudeProperty =
        AvaloniaProperty.Register<AltitudeIndicator, double>("AIAltitude", double.NaN);
    public static readonly StyledProperty<double> AIClimbRateProperty =
        AvaloniaProperty.Register<AltitudeIndicator, double>("AIClimbRate", 0.0);
    public static readonly StyledProperty<string> AIAltFrameProperty =
        AvaloniaProperty.Register<AltitudeIndicator, string>("AIAltFrame", "Rel");

    static AltitudeIndicator()
    {
        AffectsRender<AltitudeIndicator>(AIAltitudeProperty, AIClimbRateProperty, AIAltFrameProperty);
    }

    public double AIAltitude  { get => GetValue(AIAltitudeProperty);  set => SetValue(AIAltitudeProperty, value); }
    public double AIClimbRate { get => GetValue(AIClimbRateProperty); set => SetValue(AIClimbRateProperty, value); }
    public string AIAltFrame  { get => GetValue(AIAltFrameProperty);  set => SetValue(AIAltFrameProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        // Frame label (top-left tiny)
        var frameFt = new FormattedText(AIAltFrame,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.6, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(frameFt, new Point(dfw * 0.3, 1));

        // Altitude value (centre)
        string altTxt = double.IsNaN(AIAltitude) ? "—" : $"{AIAltitude:F1}";
        var altFt = new FormattedText(altTxt,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 1.0, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(altFt, new Point((w - altFt.Width) / 2, (h - altFt.Height) / 2));

        // Climb-rate arrow (right side)
        double cr  = AIClimbRate;
        if (Math.Abs(cr) < 0.1) return;
        bool   up   = cr > 0;
        Color  arC  = up ? QgcColors.ColorGreen : QgcColors.ColorRed;
        double arX  = w - dfw * 1.2;
        double arCy = h / 2;
        double arLen = Math.Min(h * 0.35, Math.Abs(cr) * 4);
        arLen = Math.Clamp(arLen, 4, h * 0.4);
        double tipY = up ? arCy - arLen : arCy + arLen;
        double baseY= up ? arCy + arLen * 0.3 : arCy - arLen * 0.3;
        var pen = new Pen(new SolidColorBrush(arC), 1.5);
        dc.DrawLine(pen, new Point(arX, baseY), new Point(arX, tipY));
        // Arrow head
        double hSz = 4;
        double dy  = up ? hSz : -hSz;
        dc.DrawLine(pen, new Point(arX, tipY), new Point(arX - 3, tipY + dy));
        dc.DrawLine(pen, new Point(arX, tipY), new Point(arX + 3, tipY + dy));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 90 : available.Width;
        double h = double.IsInfinity(available.Height) ? 36 : available.Height;
        return new Size(w, h);
    }
}

// ── #216 DistanceToHomeIndicator ──────────────────────────────────────────────
// Shows the distance from vehicle to home point.
// DTHDistance in metres; DTHIsHomeSet controls visibility of the value.
// Draws a small house icon (5-line vector) left of the numeric value.
public sealed class DistanceToHomeIndicator : Control
{
    public static readonly StyledProperty<double> DTHDistanceProperty =
        AvaloniaProperty.Register<DistanceToHomeIndicator, double>("DTHDistance", 0.0);
    public static readonly StyledProperty<bool>   DTHIsHomeSetProperty =
        AvaloniaProperty.Register<DistanceToHomeIndicator, bool>("DTHIsHomeSet", false);

    static DistanceToHomeIndicator()
    {
        AffectsRender<DistanceToHomeIndicator>(DTHDistanceProperty, DTHIsHomeSetProperty);
    }

    public double DTHDistance  { get => GetValue(DTHDistanceProperty);  set => SetValue(DTHDistanceProperty, value); }
    public bool   DTHIsHomeSet { get => GetValue(DTHIsHomeSetProperty); set => SetValue(DTHIsHomeSetProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        Color iconColor = DTHIsHomeSet ? QgcColors.ColorGreen : QgcColors.ColorGrey;
        var iconPen = new Pen(new SolidColorBrush(iconColor), 1.2);

        // House icon (left side, vertically centred)
        double iSz  = Math.Min(h * 0.65, dfh * 0.9);
        double iX   = dfw * 0.4;
        double iY   = (h - iSz) / 2;
        double roofH= iSz * 0.45;
        double bodyH= iSz * 0.55;
        // Roof (triangle)
        dc.DrawLine(iconPen, new Point(iX,              iY + roofH),
                             new Point(iX + iSz / 2,   iY));
        dc.DrawLine(iconPen, new Point(iX + iSz / 2,   iY),
                             new Point(iX + iSz,        iY + roofH));
        // Body (rectangle outline)
        dc.DrawLine(iconPen, new Point(iX,              iY + roofH),
                             new Point(iX,              iY + iSz));
        dc.DrawLine(iconPen, new Point(iX + iSz,        iY + roofH),
                             new Point(iX + iSz,        iY + iSz));
        dc.DrawLine(iconPen, new Point(iX,              iY + iSz),
                             new Point(iX + iSz,        iY + iSz));

        // Value text
        string txt = !DTHIsHomeSet ? "—" :
                     DTHDistance >= 1000 ? $"{DTHDistance / 1000:F2} km" : $"{DTHDistance:F0} m";
        var ft = new FormattedText(txt,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.85, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(ft, new Point(iX + iSz + dfw * 0.5, (h - ft.Height) / 2));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 100 : available.Width;
        double h = double.IsInfinity(available.Height) ? 28  : available.Height;
        return new Size(w, h);
    }
}

// ── #226 FlightTimerIndicator ─────────────────────────────────────────────────
// Live flight timer showing elapsed time in HH:MM:SS format.
// FTIElapsedSeconds drives the display; FTIIsRunning controls colour (green/grey).
// Draws the time string centred with a small "FLT" prefix label above.
public sealed class FlightTimerIndicator : Control
{
    public static readonly StyledProperty<double> FTIElapsedSecondsProperty =
        AvaloniaProperty.Register<FlightTimerIndicator, double>("FTIElapsedSeconds", 0.0);
    public static readonly StyledProperty<bool>   FTIIsRunningProperty =
        AvaloniaProperty.Register<FlightTimerIndicator, bool>("FTIIsRunning", false);

    static FlightTimerIndicator()
    {
        AffectsRender<FlightTimerIndicator>(FTIElapsedSecondsProperty, FTIIsRunningProperty);
    }

    public double FTIElapsedSeconds { get => GetValue(FTIElapsedSecondsProperty); set => SetValue(FTIElapsedSecondsProperty, value); }
    public bool   FTIIsRunning      { get => GetValue(FTIIsRunningProperty);      set => SetValue(FTIIsRunningProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;

        int totalSec = (int)Math.Max(0, FTIElapsedSeconds);
        int hh = totalSec / 3600;
        int mm = (totalSec % 3600) / 60;
        int ss = totalSec % 60;
        string timeTxt = hh > 0 ? $"{hh}:{mm:D2}:{ss:D2}" : $"{mm:D2}:{ss:D2}";

        Color valColor = FTIIsRunning ? QgcColors.ColorGreen : QgcColors.TextSecondary;

        // "FLT" prefix (tiny, top)
        var prefFt = new FormattedText("FLT",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.65, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(prefFt, new Point((w - prefFt.Width) / 2, 1));

        // Time value (large, bottom)
        var timeFt = new FormattedText(timeTxt,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 0.92, new SolidColorBrush(valColor));
        dc.DrawText(timeFt, new Point((w - timeFt.Width) / 2, h - timeFt.Height - 1));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 80 : available.Width;
        double h = double.IsInfinity(available.Height) ? 36 : available.Height;
        return new Size(w, h);
    }
}

// ── #227 SatelliteCountIndicator ──────────────────────────────────────────────
// Satellite count (large) + HDOP value (small subscript).
// SCISatCount, SCIHDOP, SCIFixType (0=no fix, 1=2D, 2=3D, 3=DGPS, 4=RTK).
// Colour: red=no-fix, orange=2D, green=3D+.
public sealed class SatelliteCountIndicator : Control
{
    public static readonly StyledProperty<int>    SCISatCountProperty =
        AvaloniaProperty.Register<SatelliteCountIndicator, int>("SCISatCount", 0);
    public static readonly StyledProperty<double> SCIHDOPProperty =
        AvaloniaProperty.Register<SatelliteCountIndicator, double>("SCIHDOP", 99.9);
    public static readonly StyledProperty<int>    SCIFixTypeProperty =
        AvaloniaProperty.Register<SatelliteCountIndicator, int>("SCIFixType", 0);

    static SatelliteCountIndicator()
    {
        AffectsRender<SatelliteCountIndicator>(SCISatCountProperty, SCIHDOPProperty, SCIFixTypeProperty);
    }

    public int    SCISatCount { get => GetValue(SCISatCountProperty); set => SetValue(SCISatCountProperty, value); }
    public double SCIHDOP     { get => GetValue(SCIHDOPProperty);     set => SetValue(SCIHDOPProperty, value); }
    public int    SCIFixType  { get => GetValue(SCIFixTypeProperty);  set => SetValue(SCIFixTypeProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;

        Color valC = SCIFixType == 0 ? QgcColors.ColorRed
                   : SCIFixType == 1 ? QgcColors.ColorOrange
                   : QgcColors.ColorGreen;

        // Sat count (large)
        var satFt = new FormattedText(SCISatCount.ToString(),
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.Bold),
            dfh * 1.1, new SolidColorBrush(valC));
        dc.DrawText(satFt, new Point((w - satFt.Width) / 2, 0));

        // HDOP (small, below)
        string hdopTxt = $"H:{SCIHDOP:F1}";
        var hdopFt = new FormattedText(hdopTxt,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.65, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(hdopFt, new Point((w - hdopFt.Width) / 2, satFt.Height + 1));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 48 : available.Width;
        double h = double.IsInfinity(available.Height) ? 36 : available.Height;
        return new Size(w, h);
    }
}

// ── #236 VehicleAlertRow ──────────────────────────────────────────────────────
// Single alert entry row: severity colour sidebar + message text + timestamp.
// VARSeverity (0–7 MAVLink severity), VARText, VARTimestamp (HH:MM:SS string).
public sealed class VehicleAlertRow : Control
{
    public static readonly StyledProperty<int>    VARSeverityProperty =
        AvaloniaProperty.Register<VehicleAlertRow, int>("VARSeverity", 6);
    public static readonly StyledProperty<string> VARTextProperty =
        AvaloniaProperty.Register<VehicleAlertRow, string>("VARText", string.Empty);
    public static readonly StyledProperty<string> VARTimestampProperty =
        AvaloniaProperty.Register<VehicleAlertRow, string>("VARTimestamp", string.Empty);

    static VehicleAlertRow()
    {
        AffectsRender<VehicleAlertRow>(VARSeverityProperty, VARTextProperty, VARTimestampProperty);
    }

    public int    VARSeverity  { get => GetValue(VARSeverityProperty);  set => SetValue(VARSeverityProperty, value); }
    public string VARText      { get => GetValue(VARTextProperty);      set => SetValue(VARTextProperty, value); }
    public string VARTimestamp { get => GetValue(VARTimestampProperty); set => SetValue(VARTimestampProperty, value); }

    private Color SeverityColor => VARSeverity switch
    {
        <= 2 => QgcColors.ColorRed,
        3    => QgcColors.ColorOrange,
        4    => Color.FromRgb(200, 180, 0),
        <= 6 => QgcColors.ColorBlue,
        _    => QgcColors.ColorGrey
    };

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        const double sideW = 4;
        dc.DrawRectangle(new SolidColorBrush(SeverityColor), null, new Rect(0, 0, sideW, h));

        double tsW = 0;
        if (!string.IsNullOrEmpty(VARTimestamp))
        {
            var tsFt = new FormattedText(VARTimestamp,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.68, new SolidColorBrush(QgcColors.TextSecondary));
            tsW = tsFt.Width + dfw * 0.5;
            dc.DrawText(tsFt, new Point(w - tsW, (h - tsFt.Height) / 2));
        }

        var msgFt = new FormattedText(VARText,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.Text))
        { MaxTextWidth = w - sideW - dfw - tsW };
        dc.DrawText(msgFt, new Point(sideW + dfw * 0.5, (h - msgFt.Height) / 2));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width) ? available.Width : 340;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}

// ── #237 EscTelemetryOverlay ──────────────────────────────────────────────────
// Compact all-ESC summary strip: N equal-width columns, each showing RPM + temp dot.
// ETOEscEntries is IReadOnlyList<EscEntry>; ETOTempWarningC is the overheat threshold.
public sealed class EscTelemetryOverlay : Control
{
    public static readonly StyledProperty<IReadOnlyList<EscEntry>?> ETOEscEntriesProperty =
        AvaloniaProperty.Register<EscTelemetryOverlay, IReadOnlyList<EscEntry>?>("ETOEscEntries", null);
    public static readonly StyledProperty<double> ETOTempWarningCProperty =
        AvaloniaProperty.Register<EscTelemetryOverlay, double>("ETOTempWarningC", 70.0);

    static EscTelemetryOverlay()
    {
        AffectsRender<EscTelemetryOverlay>(ETOEscEntriesProperty, ETOTempWarningCProperty);
        AffectsMeasure<EscTelemetryOverlay>(ETOEscEntriesProperty);
    }

    public IReadOnlyList<EscEntry>? ETOEscEntries   { get => GetValue(ETOEscEntriesProperty);   set => SetValue(ETOEscEntriesProperty, value); }
    public double                   ETOTempWarningC { get => GetValue(ETOTempWarningCProperty);  set => SetValue(ETOTempWarningCProperty, value); }

    public override void Render(DrawingContext dc)
    {
        var entries = ETOEscEntries;
        if (entries == null || entries.Count == 0) return;

        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        int    n   = entries.Count;
        double colW= w / n;

        for (int i = 0; i < n; i++)
        {
            var e  = entries[i];
            double cx = i * colW;

            if (i > 0)
                dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
                    new Point(cx, 2), new Point(cx, h - 2));

            string rpmTxt = e.IsOnline ? $"{e.Rpm / 1000.0:F1}k" : "—";
            var rpmFt = new FormattedText(rpmTxt,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
                dfh * 0.85, new SolidColorBrush(e.IsOnline ? QgcColors.Text : QgcColors.TextSecondary));
            dc.DrawText(rpmFt, new Point(cx + (colW - rpmFt.Width) / 2, h / 2 - rpmFt.Height));

            var idxFt = new FormattedText($"M{e.Index + 1}",
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.62, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(idxFt, new Point(cx + (colW - idxFt.Width) / 2, h / 2));

            double dotR = h * 0.12;
            Color  dotC = !e.IsOnline ? QgcColors.ColorGrey
                        : e.TemperatureCelsius >= ETOTempWarningC             ? QgcColors.ColorRed
                        : e.TemperatureCelsius >= ETOTempWarningC * 0.8       ? QgcColors.ColorOrange
                        : QgcColors.ColorGreen;
            dc.DrawEllipse(new SolidColorBrush(dotC), null,
                new Point(cx + colW - dotR - 2, dotR + 2), dotR, dotR);
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        int n = ETOEscEntries?.Count ?? 4;
        double w = !double.IsInfinity(available.Width) ? available.Width : n * 60.0;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight + 4);
    }
}

// ══════════════════════════════════════════════════════════════════
// #245 — DropButton / DropPanel
// QGC: src/QmlControls/DropButton.qml + DropPanel.qml
// Generic button that shows a floating panel in one of 4 directions.
// ══════════════════════════════════════════════════════════════════

public enum DropDirection { Up, Down, Left, Right }

/// <summary>
/// Floating drop panel shown relative to its host DropButton.
/// Draws a rounded-rect background; content is painted by override.
/// </summary>
public class DropPanel : Control
{
    public static readonly StyledProperty<bool>          DPIsOpenProperty =
        AvaloniaProperty.Register<DropPanel, bool>("DPIsOpen", false);
    public static readonly StyledProperty<DropDirection> DPDirectionProperty =
        AvaloniaProperty.Register<DropPanel, DropDirection>("DPDirection", DropDirection.Down);
    public static readonly StyledProperty<double>        DPPanelWidthProperty =
        AvaloniaProperty.Register<DropPanel, double>("DPPanelWidth", 200);
    public static readonly StyledProperty<double>        DPPanelHeightProperty =
        AvaloniaProperty.Register<DropPanel, double>("DPPanelHeight", 120);

    public bool          DPIsOpen      { get => GetValue(DPIsOpenProperty);      set => SetValue(DPIsOpenProperty, value); }
    public DropDirection DPDirection   { get => GetValue(DPDirectionProperty);   set => SetValue(DPDirectionProperty, value); }
    public double        DPPanelWidth  { get => GetValue(DPPanelWidthProperty);  set => SetValue(DPPanelWidthProperty, value); }
    public double        DPPanelHeight { get => GetValue(DPPanelHeightProperty); set => SetValue(DPPanelHeightProperty, value); }

    static DropPanel()
    {
        AffectsRender<DropPanel>(DPIsOpenProperty, DPDirectionProperty, DPPanelWidthProperty, DPPanelHeightProperty);
    }

    public override void Render(DrawingContext dc)
    {
        if (!DPIsOpen) return;
        double pw = DPPanelWidth;
        double ph = DPPanelHeight;
        double br = ScreenMetrics.DefaultBorderRadius;
        var brush = new SolidColorBrush(QgcColors.Window);
        var pen   = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
        dc.DrawRectangle(brush, pen, new Rect(0, 0, pw, ph), br, br);
    }

    protected override Size MeasureOverride(Size available) =>
        DPIsOpen ? new Size(DPPanelWidth, DPPanelHeight) : new Size(0, 0);
}

/// <summary>
/// Button that toggles a DropPanel in the configured direction.
/// Equivalent to QGC DropButton: text/icon + small direction arrow indicator.
/// </summary>
public sealed class DropButton : Control
{
    public static readonly StyledProperty<string>        DBButtonTextProperty =
        AvaloniaProperty.Register<DropButton, string>("DBButtonText", "");
    public static readonly StyledProperty<bool>          DBIsOpenProperty =
        AvaloniaProperty.Register<DropButton, bool>("DBIsOpen", false);
    public static readonly StyledProperty<DropDirection> DBDropDirectionProperty =
        AvaloniaProperty.Register<DropButton, DropDirection>("DBDropDirection", DropDirection.Down);
    public static readonly StyledProperty<Color>         DBButtonColorProperty =
        AvaloniaProperty.Register<DropButton, Color>("DBButtonColor", default);
    public static readonly StyledProperty<Color>         DBTextColorProperty =
        AvaloniaProperty.Register<DropButton, Color>("DBTextColor", default);

    public string        DBButtonText    { get => GetValue(DBButtonTextProperty);    set => SetValue(DBButtonTextProperty, value); }
    public bool          DBIsOpen        { get => GetValue(DBIsOpenProperty);        set => SetValue(DBIsOpenProperty, value); }
    public DropDirection DBDropDirection { get => GetValue(DBDropDirectionProperty); set => SetValue(DBDropDirectionProperty, value); }
    public Color         DBButtonColor   { get => GetValue(DBButtonColorProperty);   set => SetValue(DBButtonColorProperty, value); }
    public Color         DBTextColor     { get => GetValue(DBTextColorProperty);     set => SetValue(DBTextColorProperty, value); }

    public event EventHandler? OpenChanged;

    private bool _pressed;

    static DropButton()
    {
        AffectsRender<DropButton>(DBButtonTextProperty, DBIsOpenProperty, DBDropDirectionProperty,
                                  DBButtonColorProperty, DBTextColorProperty);
    }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double br  = ScreenMetrics.DefaultBorderRadius;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;

        Color bgC  = DBButtonColor == default ? QgcColors.Button : DBButtonColor;
        Color txtC = DBTextColor   == default ? QgcColors.ButtonText : DBTextColor;
        if (DBIsOpen || _pressed)
            bgC = Color.FromArgb(bgC.A, (byte)Math.Min(bgC.R + 30, 255),
                                        (byte)Math.Min(bgC.G + 30, 255),
                                        (byte)Math.Min(bgC.B + 30, 255));

        dc.DrawRectangle(new SolidColorBrush(bgC),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1),
            new Rect(0, 0, w, h), br, br);

        // Text
        if (!string.IsNullOrEmpty(DBButtonText))
        {
            const double arrowW = 12;
            var ft = new FormattedText(DBButtonText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75, new SolidColorBrush(txtC));
            dc.DrawText(ft, new Point((w - arrowW - ft.Width) / 2, (h - ft.Height) / 2));
        }

        // Direction arrow
        double ax = w - 10;
        double ay = h / 2;
        double asz = 4.0;
        var arrowBrush = new SolidColorBrush(txtC);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            switch (DBDropDirection)
            {
                case DropDirection.Down:
                    ctx.BeginFigure(new Point(ax - asz, ay - asz * 0.5), true);
                    ctx.LineTo(new Point(ax + asz, ay - asz * 0.5));
                    ctx.LineTo(new Point(ax, ay + asz * 0.5));
                    break;
                case DropDirection.Up:
                    ctx.BeginFigure(new Point(ax - asz, ay + asz * 0.5), true);
                    ctx.LineTo(new Point(ax + asz, ay + asz * 0.5));
                    ctx.LineTo(new Point(ax, ay - asz * 0.5));
                    break;
                case DropDirection.Right:
                    ctx.BeginFigure(new Point(ax - asz * 0.5, ay - asz), true);
                    ctx.LineTo(new Point(ax - asz * 0.5, ay + asz));
                    ctx.LineTo(new Point(ax + asz * 0.5, ay));
                    break;
                case DropDirection.Left:
                    ctx.BeginFigure(new Point(ax + asz * 0.5, ay - asz), true);
                    ctx.LineTo(new Point(ax + asz * 0.5, ay + asz));
                    ctx.LineTo(new Point(ax - asz * 0.5, ay));
                    break;
            }
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(arrowBrush, null, geo);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressed = true;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed && Bounds.Contains(e.GetPosition(this)))
        {
            DBIsOpen = !DBIsOpen;
            OpenChanged?.Invoke(this, EventArgs.Empty);
        }
        _pressed = false;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width) ? available.Width : 100;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}