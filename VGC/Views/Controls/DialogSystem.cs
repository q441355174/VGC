using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Globalization;
using System.Linq;

namespace VGC.Views.Controls;

/// <summary>
/// Modal popup dialog — equivalent to QGC QGCPopupDialog.
/// Full-screen dark overlay with centered content panel + title bar + action buttons.
/// </summary>
public class PopupDialog : ContentControl
{
    public static readonly StyledProperty<string> DialogTitleProperty =
        AvaloniaProperty.Register<PopupDialog, string>(nameof(DialogTitle), "");

    public static readonly StyledProperty<bool> ShowAcceptButtonProperty =
        AvaloniaProperty.Register<PopupDialog, bool>(nameof(ShowAcceptButton), true);

    public static readonly StyledProperty<bool> ShowCancelButtonProperty =
        AvaloniaProperty.Register<PopupDialog, bool>(nameof(ShowCancelButton), true);

    public static readonly StyledProperty<string> AcceptTextProperty =
        AvaloniaProperty.Register<PopupDialog, string>(nameof(AcceptText), "OK");

    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<PopupDialog, string>(nameof(CancelText), "Cancel");

    public string DialogTitle { get => GetValue(DialogTitleProperty); set => SetValue(DialogTitleProperty, value); }
    public bool ShowAcceptButton { get => GetValue(ShowAcceptButtonProperty); set => SetValue(ShowAcceptButtonProperty, value); }
    public bool ShowCancelButton { get => GetValue(ShowCancelButtonProperty); set => SetValue(ShowCancelButtonProperty, value); }
    public string AcceptText { get => GetValue(AcceptTextProperty); set => SetValue(AcceptTextProperty, value); }
    public string CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    public event EventHandler? Accepted;
    public event EventHandler? Cancelled;
    public event EventHandler? Closed;

    public void Accept()
    {
        Accepted?.Invoke(this, EventArgs.Empty);
        Close();
    }

    public void Cancel()
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        Close();
    }

    public void Close()
    {
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Open()
    {
        IsVisible = true;
    }
}

/// <summary>
/// Toast notification bar — equivalent to QGC validationErrorToast.
/// Shows at bottom-center, auto-dismisses after timeout.
/// </summary>
public sealed class ToastNotification
{
    private readonly List<ToastMessage> _messages = [];
    private int _idCounter;

    public event EventHandler? MessagesChanged;

    public IReadOnlyList<ToastMessage> ActiveMessages => _messages.Where(m => !m.Dismissed).ToArray();

    public ToastMessage Show(string text, ToastSeverity severity = ToastSeverity.Info, int timeoutMs = 3000)
    {
        var message = new ToastMessage(++_idCounter, text, severity, timeoutMs, DateTime.Now);
        _messages.Add(message);
        MessagesChanged?.Invoke(this, EventArgs.Empty);

        if (timeoutMs > 0)
        {
            _ = DismissAfterDelay(message, timeoutMs);
        }

        return message;
    }

    public void Dismiss(int id)
    {
        var msg = _messages.FirstOrDefault(m => m.Id == id);
        if (msg is not null)
        {
            msg.Dismissed = true;
            MessagesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        foreach (var msg in _messages) msg.Dismissed = true;
        MessagesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task DismissAfterDelay(ToastMessage message, int delayMs)
    {
        await Task.Delay(delayMs).ConfigureAwait(false);
        message.Dismissed = true;
        MessagesChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class ToastMessage
{
    public ToastMessage(int id, string text, ToastSeverity severity, int timeoutMs, DateTime createdAt)
    {
        Id = id;
        Text = text;
        Severity = severity;
        TimeoutMs = timeoutMs;
        CreatedAt = createdAt;
    }

    public int Id { get; }
    public string Text { get; }
    public ToastSeverity Severity { get; }
    public int TimeoutMs { get; }
    public DateTime CreatedAt { get; }
    public bool Dismissed { get; set; }
}

public enum ToastSeverity
{
    Info,
    Warning,
    Error,
    Success
}

/// <summary>
/// Navigation guard — equivalent to QGC allowViewSwitch() / navigationBlockedReason.
/// Prevents view switching during calibration, parameter editing errors, etc.
/// </summary>
public sealed class NavigationGuard
{
    private string _blockedReason = "";
    private int _validationErrorCount;

    public event EventHandler? StateChanged;

    public bool IsBlocked => !string.IsNullOrEmpty(_blockedReason) || _validationErrorCount > 0;

    public string BlockedReason => _blockedReason;

    public int ValidationErrorCount => _validationErrorCount;

    public string BlockedMessage => !string.IsNullOrEmpty(_blockedReason)
        ? _blockedReason
        : _validationErrorCount > 0
            ? $"Please correct {_validationErrorCount} invalid value(s) before continuing"
            : "";

    /// <summary>
    /// Block navigation with a reason (e.g., "Calibration in progress").
    /// </summary>
    public void Block(string reason)
    {
        _blockedReason = reason;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Unblock navigation.
    /// </summary>
    public void Unblock()
    {
        _blockedReason = "";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Increment validation error count (called when a FactTextField has an error).
    /// </summary>
    public void AddValidationError()
    {
        _validationErrorCount++;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Decrement validation error count (called when a FactTextField error is resolved).
    /// </summary>
    public void RemoveValidationError()
    {
        _validationErrorCount = Math.Max(0, _validationErrorCount - 1);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Check if navigation is allowed. Returns true if OK, false if blocked.
    /// </summary>
    public bool AllowViewSwitch() => !IsBlocked;

    /// <summary>
    /// Reset all blocks and errors.
    /// </summary>
    public void Reset()
    {
        _blockedReason = "";
        _validationErrorCount = 0;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Guided value slider for altitude/distance input — equivalent to QGC GuidedValueSlider.
/// Vertical flick-scroll slider with tick marks for setting takeoff altitude, change altitude, etc.
/// </summary>
public class GuidedValueSlider : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<GuidedValueSlider, double>(nameof(Value), 10);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<GuidedValueSlider, double>(nameof(Minimum), 0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<GuidedValueSlider, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<GuidedValueSlider, double>(nameof(Step), 5);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<GuidedValueSlider, string>(nameof(Title), "Altitude");

    public static readonly StyledProperty<string> UnitsProperty =
        AvaloniaProperty.Register<GuidedValueSlider, string>(nameof(Units), "m");

    static GuidedValueSlider()
    {
        AffectsRender<GuidedValueSlider>(ValueProperty, MinimumProperty, MaximumProperty, StepProperty, TitleProperty, UnitsProperty);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum)); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }

    public event EventHandler<double>? ValueCommitted;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 20 || bounds.Height < 50) return;

        var w = bounds.Width;
        var h = bounds.Height;
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var boldTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);

        // Background
        var bgBrush = new SolidColorBrush(Color.Parse("#E60d1a24"));
        context.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

        // Title
        var titleText = new FormattedText(Title, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 11, new SolidColorBrush(Color.Parse("#91a4b5")));
        context.DrawText(titleText, new Point((w - titleText.Width) / 2, 4));

        // Gauge area
        var gaugeTop = 24.0;
        var gaugeBottom = h - 30;
        var gaugeHeight = gaugeBottom - gaugeTop;
        var centerY = (gaugeTop + gaugeBottom) / 2;

        // Scale
        var range = Maximum - Minimum;
        if (range <= 0) return;
        var pixelsPerUnit = gaugeHeight / range;

        var tickPen = new Pen(Brushes.White, 1);
        var minorPen = new Pen(new SolidColorBrush(Color.Parse("#4e6070")), 0.5);

        // Tick marks
        for (var v = Minimum; v <= Maximum; v += Step)
        {
            var y = gaugeBottom - (v - Minimum) * pixelsPerUnit;
            var isMajor = Math.Abs(v % (Step * 2)) < 0.001 || v == Minimum || v == Maximum;
            var tickWidth = isMajor ? w * 0.35 : w * 0.2;
            context.DrawLine(isMajor ? tickPen : minorPen, new Point(0, y), new Point(tickWidth, y));

            if (isMajor)
            {
                var label = new FormattedText(v.ToString("F0"), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, 10, Brushes.White);
                context.DrawText(label, new Point(tickWidth + 3, y - label.Height / 2));
            }
        }

        // Current value indicator
        var valueY = gaugeBottom - (Value - Minimum) * pixelsPerUnit;
        var boxH = 22.0;
        var boxBrush = new SolidColorBrush(Color.Parse("#1a3040"));
        var boxPen = new Pen(new SolidColorBrush(Color.Parse("#3498db")), 2);
        context.DrawRectangle(boxBrush, boxPen, new Rect(2, valueY - boxH / 2, w - 4, boxH), 3, 3);

        var valueText = new FormattedText($"{Value:F1} {Units}", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, boldTypeface, 12, new SolidColorBrush(Color.Parse("#3498db")));
        context.DrawText(valueText, new Point((w - valueText.Width) / 2, valueY - valueText.Height / 2));
    }

    private bool _dragging;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragging = true;
        e.Pointer.Capture(this);
        UpdateValueFromPointer(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging) UpdateValueFromPointer(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            ValueCommitted?.Invoke(this, Value);
        }
    }

    private void UpdateValueFromPointer(Point pos)
    {
        var gaugeTop = 24.0;
        var gaugeBottom = Bounds.Height - 30;
        var gaugeHeight = gaugeBottom - gaugeTop;
        if (gaugeHeight <= 0) return;

        var ratio = 1.0 - (pos.Y - gaugeTop) / gaugeHeight;
        var raw = Minimum + ratio * (Maximum - Minimum);
        var snapped = Math.Round(raw / Step) * Step;
        Value = Math.Clamp(snapped, Minimum, Maximum);
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 80 : Math.Min(availableSize.Width, 100);
        var h = double.IsInfinity(availableSize.Height) ? 400 : availableSize.Height;
        return new Size(w, h);
    }
}

// ════════════════════════════════════════════════════════════════
// SLIDER SWITCH
// QGC equivalent: QmlControls/SliderSwitch.qml
// Slide-to-confirm pill control (slide or hold space-bar to activate).
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Slide-to-confirm control — pill-shaped background with a draggable circle.
/// Drag or animate the circle fully to the right to fire the <see cref="Accepted"/> event.
/// Equivalent to QGC QmlControls/SliderSwitch.qml
/// </summary>
public sealed class SliderSwitch : Control
{
    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<SliderSwitch, string>(nameof(ConfirmText), "Slide to confirm");

    static SliderSwitch()
    {
        AffectsRender<SliderSwitch>(ConfirmTextProperty);
    }

    public string ConfirmText
    {
        get => GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    public event EventHandler? Accepted;

    private double _thumbX;
    private bool _dragging;
    private Point _dragStart;
    private double _dragStartThumbX;

    private double Border     => 4;
    private double Diameter   => Bounds.Height - Border * 2;
    private double DragStartX => Border;
    private double DragStopX  => Bounds.Width - Diameter - Border;

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 20 || h < 12) return;

        // Pill background
        var bgBrush = new SolidColorBrush(QgcColors.WindowShade);
        context.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h), h / 2, h / 2);

        // Confirm label (centered)
        var tf = new Typeface("Segoe UI");
        var textColor = new SolidColorBrush(QgcColors.ButtonText);
        var fmt = new FormattedText(ConfirmText, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, tf, ScreenMetrics.DefaultFontPixelHeight, textColor);
        context.DrawText(fmt, new Point(Diameter + Border + 8, (h - fmt.Height) / 2));

        // Draggable thumb circle
        var thumbColor = new SolidColorBrush(QgcColors.PrimaryButton);
        var tx = Math.Clamp(_thumbX, DragStartX, DragStopX);
        context.DrawEllipse(thumbColor, null,
            new Point(tx + Diameter / 2, h / 2), Diameter / 2, Diameter / 2);

        // Arrow right on thumb (simple ">" drawn as lines)
        var arrowPen = new Pen(new SolidColorBrush(QgcColors.ButtonText), 2);
        var ax = tx + Diameter / 2;
        var ay = h / 2;
        var aw = Diameter * 0.18;
        var ah = Diameter * 0.28;
        context.DrawLine(arrowPen, new Point(ax - aw, ay - ah), new Point(ax + aw, ay));
        context.DrawLine(arrowPen, new Point(ax + aw, ay), new Point(ax - aw, ay + ah));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var tx = Math.Clamp(_thumbX, DragStartX, DragStopX);
        // Only start drag if pointer is on the thumb
        if (Math.Abs(pos.X - (tx + Diameter / 2)) <= Diameter / 2 + 8)
        {
            _dragging = true;
            _dragStart = pos;
            _dragStartThumbX = tx;
            e.Pointer.Capture(this);
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        var pos = e.GetPosition(this);
        _thumbX = Math.Clamp(_dragStartThumbX + (pos.X - _dragStart.X), DragStartX, DragStopX);
        InvalidateVisual();

        if (_thumbX >= DragStopX - 2)
            ConfirmAndReset();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            _thumbX = DragStartX;
            InvalidateVisual();
        }
    }

    private void ConfirmAndReset()
    {
        _dragging = false;
        _thumbX = DragStartX;
        InvalidateVisual();
        Accepted?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? 44 : Math.Min(availableSize.Height, 60);
        return new Size(w, h);
    }
}

// ════════════════════════════════════════════════════════════════
// SELECT VIEW DROPDOWN
// QGC equivalent: Toolbar/SelectViewDropdown.qml
// Navigation panel exposed via a ToolIndicatorPage.
// ════════════════════════════════════════════════════════════════

/// <summary>
/// View-selection dropdown panel: Fly / Plan / Analyze / Setup / Settings / Close.
/// Shown when the main status/logo button in the toolbar is clicked.
/// Equivalent to QGC Toolbar/SelectViewDropdown.qml
/// </summary>
public sealed class SelectViewDropdown : Control
{
    public event EventHandler? FlyRequested;
    public event EventHandler? PlanRequested;
    public event EventHandler? AnalyzeRequested;
    public event EventHandler? SetupRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? CloseRequested;

    private static readonly string[] _labels   = ["Fly", "Plan", "Analyze", "Setup", "Settings", "Close"];
    private static readonly string[] _icons     = ["✈", "📋", "🔍", "⚙", "≡", "✕"];
    private int _hoverIndex = -1;

    private double ButtonHeight => ScreenMetrics.DefaultFontPixelHeight * 3;
    private double ButtonWidth  => ScreenMetrics.DefaultFontPixelWidth * 18;

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        if (w < 20) return;

        var tf     = new Typeface("Segoe UI");
        var tfBold = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var bh = ButtonHeight;

        for (var i = 0; i < _labels.Length; i++)
        {
            var y = i * (bh + 2);
            var isHover = i == _hoverIndex;

            var bg = isHover
                ? new SolidColorBrush(QgcColors.PrimaryButtonFill)
                : new SolidColorBrush(QgcColors.ButtonFill);
            context.DrawRectangle(bg, null, new Rect(0, y, w, bh), 4, 4);

            var icon = new FormattedText(_icons[i], CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, ScreenMetrics.DefaultFontPixelHeight, new SolidColorBrush(QgcColors.ButtonText));
            context.DrawText(icon, new Point(10, y + (bh - icon.Height) / 2));

            var label = new FormattedText(_labels[i], CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tfBold, ScreenMetrics.DefaultFontPixelHeight, new SolidColorBrush(QgcColors.ButtonText));
            context.DrawText(label, new Point(36, y + (bh - label.Height) / 2));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var bh = ButtonHeight + 2;
        var idx = (int)(e.GetPosition(this).Y / bh);
        if (idx != _hoverIndex)
        {
            _hoverIndex = (idx >= 0 && idx < _labels.Length) ? idx : -1;
            InvalidateVisual();
        }
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
        var bh = ButtonHeight + 2;
        var idx = (int)(e.GetPosition(this).Y / bh);
        EventHandler? handler = idx switch
        {
            0 => FlyRequested,
            1 => PlanRequested,
            2 => AnalyzeRequested,
            3 => SetupRequested,
            4 => SettingsRequested,
            5 => CloseRequested,
            _ => null
        };
        handler?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var bh = ButtonHeight;
        var w  = double.IsInfinity(availableSize.Width) ? ButtonWidth : availableSize.Width;
        return new Size(w, _labels.Length * (bh + 2));
    }
}

// ════════════════════════════════════════════════════════════════
// CENTER MAP DROP PANEL
// QGC equivalent: FlightMap/Widgets/CenterMapDropPanel.qml
// ════════════════════════════════════════════════════════════════

/// <summary>
/// "Center map on" popup panel with 6 destination buttons.
/// Equivalent to QGC FlightMap/Widgets/CenterMapDropPanel.qml
/// </summary>
public sealed class CenterMapDropPanel : Control
{
    public event EventHandler? MissionRequested;
    public event EventHandler? AllItemsRequested;
    public event EventHandler? HomeRequested;
    public event EventHandler? VehicleRequested;
    public event EventHandler? CurrentLocationRequested;
    public event EventHandler? SpecifiedLocationRequested;

    public static readonly StyledProperty<bool> HasVehicleProperty =
        AvaloniaProperty.Register<CenterMapDropPanel, bool>(nameof(HasVehicle));

    public static readonly StyledProperty<bool> HasCurrentLocationProperty =
        AvaloniaProperty.Register<CenterMapDropPanel, bool>(nameof(HasCurrentLocation));

    public bool HasVehicle          { get => GetValue(HasVehicleProperty);          set => SetValue(HasVehicleProperty, value); }
    public bool HasCurrentLocation  { get => GetValue(HasCurrentLocationProperty);  set => SetValue(HasCurrentLocationProperty, value); }

    private readonly string[] _labels  = ["Mission", "All items", "Home", "Vehicle", "Current Location", "Specified Location"];
    private int _hoverIndex = -1;

    private double ButtonHeight => ScreenMetrics.DefaultFontPixelHeight * 2.5;

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        if (w < 20) return;

        // Header
        var headerTf = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var headerFmt = new FormattedText("Center map on:", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, headerTf, ScreenMetrics.DefaultFontPixelHeight,
            new SolidColorBrush(QgcColors.Text));
        context.DrawText(headerFmt, new Point(8, 8));

        var tf = new Typeface("Segoe UI");
        var bh = ButtonHeight;
        var yOff = headerFmt.Height + 16;

        for (var i = 0; i < _labels.Length; i++)
        {
            var y = yOff + i * (bh + 2);
            var isHover = i == _hoverIndex;
            var isDisabled = (i == 3 && !HasVehicle) || (i == 4 && !HasCurrentLocation);

            var bg = isDisabled ? new SolidColorBrush(QgcColors.DisabledText) :
                     isHover    ? new SolidColorBrush(QgcColors.PrimaryButtonFill) :
                                  new SolidColorBrush(QgcColors.ButtonFill);
            context.DrawRectangle(bg, null, new Rect(0, y, w, bh), 4, 4);

            var txtBrush = new SolidColorBrush(isDisabled ? QgcColors.Text : QgcColors.ButtonText);
            var lbl = new FormattedText(_labels[i], CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, ScreenMetrics.DefaultFontPixelHeight, txtBrush);
            context.DrawText(lbl, new Point(10, y + (bh - lbl.Height) / 2));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var bh = ButtonHeight + 2;
        var headerH = ScreenMetrics.DefaultFontPixelHeight + 16;
        var idx = (int)((e.GetPosition(this).Y - headerH) / bh);
        _hoverIndex = (idx >= 0 && idx < _labels.Length) ? idx : -1;
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
        var bh = ButtonHeight + 2;
        var headerH = ScreenMetrics.DefaultFontPixelHeight + 16;
        var idx = (int)((e.GetPosition(this).Y - headerH) / bh);

        var disabled = (idx == 3 && !HasVehicle) || (idx == 4 && !HasCurrentLocation);
        if (!disabled)
        {
            EventHandler? handler = idx switch
            {
                0 => MissionRequested,
                1 => AllItemsRequested,
                2 => HomeRequested,
                3 => VehicleRequested,
                4 => CurrentLocationRequested,
                5 => SpecifiedLocationRequested,
                _ => null
            };
            handler?.Invoke(this, EventArgs.Empty);
        }
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var headerH = ScreenMetrics.DefaultFontPixelHeight + 16;
        var w = double.IsInfinity(availableSize.Width) ? ScreenMetrics.DefaultFontPixelWidth * 22 : availableSize.Width;
        return new Size(w, headerH + _labels.Length * (ButtonHeight + 2));
    }
}

// ════════════════════════════════════════════════════════════════
// FLY VIEW ADDITIONAL ACTIONS PANEL
// QGC equivalent: FlyView/FlyViewAdditionalActionsPanel.qml
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Single action entry shown in the additional-actions drop panel.
/// Equivalent to one item in QGC FlyViewAdditionalActionsPanel.
/// </summary>
public sealed record GuidedActionEntry(string Title, int ActionId, bool IsVisible = true);

/// <summary>
/// Drop panel that lists additional guided actions (predefined + MAVLink custom).
/// Equivalent to QGC FlyView/FlyViewAdditionalActionsPanel.qml
/// Actions are rendered as full-width buttons; invisible items are skipped.
/// </summary>
public sealed class FlyViewAdditionalActionsPanel : Control
{
    public static readonly StyledProperty<IReadOnlyList<GuidedActionEntry>?> ActionsProperty =
        AvaloniaProperty.Register<FlyViewAdditionalActionsPanel, IReadOnlyList<GuidedActionEntry>?>(nameof(Actions));

    static FlyViewAdditionalActionsPanel()
    {
        AffectsRender<FlyViewAdditionalActionsPanel>(ActionsProperty);
    }

    public IReadOnlyList<GuidedActionEntry>? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public event EventHandler<int>? ActionRequested;

    private double ButtonHeight => ScreenMetrics.DefaultFontPixelHeight * 2.5;
    private int _hoverIndex = -1;

    private IReadOnlyList<GuidedActionEntry> VisibleActions =>
        Actions?.Where(a => a.IsVisible).ToList() ?? [];

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        if (w < 20) return;

        var tf = new Typeface("Segoe UI");
        var bh = ButtonHeight;
        var visible = VisibleActions;

        for (var i = 0; i < visible.Count; i++)
        {
            var y    = i * (bh + 2);
            var bg   = i == _hoverIndex
                ? new SolidColorBrush(QgcColors.PrimaryButtonFill)
                : new SolidColorBrush(QgcColors.ButtonFill);
            context.DrawRectangle(bg, null, new Rect(0, y, w, bh), 4, 4);

            var lbl = new FormattedText(visible[i].Title, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, ScreenMetrics.DefaultFontPixelHeight,
                new SolidColorBrush(QgcColors.ButtonText));
            context.DrawText(lbl, new Point(10, y + (bh - lbl.Height) / 2));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var bh = ButtonHeight + 2;
        var idx = (int)(e.GetPosition(this).Y / bh);
        _hoverIndex = (idx >= 0 && idx < VisibleActions.Count) ? idx : -1;
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
        var bh = ButtonHeight + 2;
        var idx = (int)(e.GetPosition(this).Y / bh);
        var visible = VisibleActions;
        if (idx >= 0 && idx < visible.Count)
            ActionRequested?.Invoke(this, visible[idx].ActionId);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = VisibleActions.Count;
        var w = double.IsInfinity(availableSize.Width) ? ScreenMetrics.DefaultFontPixelWidth * 20 : availableSize.Width;
        return new Size(w, count == 0 ? 0 : count * (ButtonHeight + 2));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SectionHeader  (#134 equivalent — QGC QGCLabel used as bold section divider)
// A simple one-line text label rendered bold + accent color, with an optional
// hairline separator beneath it.  TemplatedControl so AXAML themes can restyle.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SectionHeader : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<string> HeaderTextProperty =
        AvaloniaProperty.Register<SectionHeader, string>(nameof(HeaderText), string.Empty);

    public static readonly StyledProperty<bool> ShowSeparatorProperty =
        AvaloniaProperty.Register<SectionHeader, bool>(nameof(ShowSeparator), true);

    public string HeaderText  { get => GetValue(HeaderTextProperty);  set => SetValue(HeaderTextProperty, value); }
    public bool   ShowSeparator { get => GetValue(ShowSeparatorProperty); set => SetValue(ShowSeparatorProperty, value); }
}

// ─────────────────────────────────────────────────────────────────────────────
// SettingsGroupLayout  (#135 — QGC SettingsGroupLayout: label column + control column)
// A two-column layout panel whose items are expected to be placed via attached
// properties or by templates.  Exposes ColumnSpacing and LabelColumnWidth so
// that concrete settings panels can tune alignment without subclassing.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SettingsGroupLayout : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<double> LabelColumnWidthProperty =
        AvaloniaProperty.Register<SettingsGroupLayout, double>(nameof(LabelColumnWidth), 200.0);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<SettingsGroupLayout, double>(nameof(ColumnSpacing), 8.0);

    public static readonly StyledProperty<string> GroupTitleProperty =
        AvaloniaProperty.Register<SettingsGroupLayout, string>(nameof(GroupTitle), string.Empty);

    public double LabelColumnWidth { get => GetValue(LabelColumnWidthProperty); set => SetValue(LabelColumnWidthProperty, value); }
    public double ColumnSpacing    { get => GetValue(ColumnSpacingProperty);    set => SetValue(ColumnSpacingProperty, value); }
    public string GroupTitle       { get => GetValue(GroupTitleProperty);        set => SetValue(GroupTitleProperty, value); }
}

// ─────────────────────────────────────────────────────────────────────────────
// QgcGroupBox  (#136 — QGC QGCGroupBox: titled border container)
// Renders a rounded border with a title label inset into the top edge, similar
// to WinForms GroupBox.  Content is supplied via ContentPresenter.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class QgcGroupBox : Avalonia.Controls.ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<QgcGroupBox, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<double> CornerRadiusValueProperty =
        AvaloniaProperty.Register<QgcGroupBox, double>(nameof(CornerRadiusValue), 4.0);

    public string Title             { get => GetValue(TitleProperty);             set => SetValue(TitleProperty, value); }
    public double CornerRadiusValue { get => GetValue(CornerRadiusValueProperty); set => SetValue(CornerRadiusValueProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var bounds = new Rect(Bounds.Size);
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var r   = CornerRadiusValue;

        // Border
        var borderPen = new Pen(new SolidColorBrush(QgcColors.ColorGrey), 1.0);
        ctx.DrawRectangle(null, borderPen, bounds.Deflate(0.5), r, r);

        // Title background cutout and text
        if (!string.IsNullOrEmpty(Title))
        {
            var ft = new FormattedText(
                Title,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                dfh * 0.9,
                new SolidColorBrush(QgcColors.ColorGrey));
            double tx = dfw * 1.5;
            double ty = 0 - ft.Height / 2.0;
            // white/dark background to "cut" the border line
            var bgBrush = new SolidColorBrush(QgcColors.Window);
            ctx.DrawRectangle(bgBrush, null, new Rect(tx - 2, 0, ft.Width + 4, ft.Height / 2.0 + 1));
            ctx.DrawText(ft, new Point(tx, ty));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var inner = base.MeasureOverride(availableSize);
        var dfh   = ScreenMetrics.DefaultFontPixelHeight;
        return new Size(inner.Width, inner.Height + dfh);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        // Shift content down by half font height so title doesn't overlap content
        if (Content is Avalonia.Controls.Control child)
            child.Arrange(new Rect(0, dfh * 0.5, finalSize.Width, finalSize.Height - dfh * 0.5));
        return finalSize;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SeparatorLine  (#147 — QGC separator / divider line)
// A thin horizontal (default) or vertical line used to divide groups of
// controls.  Thickness and color are configurable.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SeparatorLine : Control
{
    public static readonly StyledProperty<Orientation> SeparatorOrientationProperty =
        AvaloniaProperty.Register<SeparatorLine, Orientation>(nameof(SeparatorOrientation), Orientation.Horizontal);
    public static readonly StyledProperty<Color> LineColorProperty =
        AvaloniaProperty.Register<SeparatorLine, Color>(nameof(LineColor), QgcColors.GroupBorder);
    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<SeparatorLine, double>(nameof(LineThickness), 1.0);

    static SeparatorLine()
    {
        AffectsRender<SeparatorLine>(SeparatorOrientationProperty, LineColorProperty, LineThicknessProperty);
    }

    public Orientation SeparatorOrientation { get => GetValue(SeparatorOrientationProperty); set => SetValue(SeparatorOrientationProperty, value); }
    public Color       LineColor            { get => GetValue(LineColorProperty);             set => SetValue(LineColorProperty, value); }
    public double      LineThickness        { get => GetValue(LineThicknessProperty);         set => SetValue(LineThicknessProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var pen = new Pen(new SolidColorBrush(LineColor), LineThickness);
        double w = Bounds.Width, h = Bounds.Height;
        if (SeparatorOrientation == Orientation.Horizontal)
            ctx.DrawLine(pen, new Point(0, h / 2), new Point(w, h / 2));
        else
            ctx.DrawLine(pen, new Point(w / 2, 0), new Point(w / 2, h));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double t = LineThickness;
        if (SeparatorOrientation == Orientation.Horizontal)
        {
            double w = double.IsInfinity(availableSize.Width) ? 100 : availableSize.Width;
            return new Size(w, t + 2);
        }
        else
        {
            double h = double.IsInfinity(availableSize.Height) ? 100 : availableSize.Height;
            return new Size(t + 2, h);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// QGCCheckableButton  (#148 — toggle button with colored indicator dot)
// A button that maintains checked/unchecked state.  Renders a colored dot on
// the left edge, button text, and a subtle hover rectangle.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class QGCCheckableButton : Control
{
    public static readonly StyledProperty<string> CheckableButtonTextProperty =
        AvaloniaProperty.Register<QGCCheckableButton, string>(nameof(ButtonText), string.Empty);
    public static readonly StyledProperty<bool>   CheckedProperty =
        AvaloniaProperty.Register<QGCCheckableButton, bool>(nameof(IsChecked), false);
    public static readonly StyledProperty<Color>  CheckedColorProperty =
        AvaloniaProperty.Register<QGCCheckableButton, Color>(nameof(CheckedColor), QgcColors.ColorGreen);
    public static readonly StyledProperty<Color>  UncheckedColorProperty =
        AvaloniaProperty.Register<QGCCheckableButton, Color>(nameof(UncheckedColor), QgcColors.ColorGrey);

    static QGCCheckableButton()
    {
        AffectsRender<QGCCheckableButton>(CheckableButtonTextProperty, CheckedProperty, CheckedColorProperty, UncheckedColorProperty);
        FocusableProperty.OverrideMetadata<QGCCheckableButton>(new StyledPropertyMetadata<bool>(true));
    }

    public string ButtonText    { get => GetValue(CheckableButtonTextProperty); set => SetValue(CheckableButtonTextProperty, value); }
    public bool   IsChecked     { get => GetValue(CheckedProperty);             set => SetValue(CheckedProperty, value); }
    public Color  CheckedColor  { get => GetValue(CheckedColorProperty);        set => SetValue(CheckedColorProperty, value); }
    public Color  UncheckedColor { get => GetValue(UncheckedColorProperty);     set => SetValue(UncheckedColorProperty, value); }

    public event EventHandler<bool>? CheckedChanged;

    private bool _hovered;

    public override void Render(DrawingContext ctx)
    {
        var dfw    = ScreenMetrics.DefaultFontPixelWidth;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);
        var w      = bounds.Width;
        var h      = bounds.Height;

        // Hover background
        if (_hovered)
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), null, bounds, 3, 3);

        // Indicator dot
        double dotR  = 4;
        double dotCx = dfw * 0.8 + dotR;
        double dotCy = h / 2;
        var dotColor = IsChecked ? CheckedColor : UncheckedColor;
        ctx.DrawEllipse(new SolidColorBrush(dotColor), null, new Point(dotCx, dotCy), dotR, dotR);

        // Button border
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(QgcColors.Button), 1), bounds.Deflate(0.5), 3, 3);

        // Text
        if (!string.IsNullOrEmpty(ButtonText))
        {
            var ft = new FormattedText(ButtonText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(QgcColors.ButtonText));
            double tx = dotCx + dotR + dfw * 0.5;
            double ty = (h - ft.Height) / 2;
            ctx.DrawText(ft, new Point(tx, ty));
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
            IsChecked = !IsChecked;
            CheckedChanged?.Invoke(this, IsChecked);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var ft  = new FormattedText(ButtonText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9, new SolidColorBrush(QgcColors.ButtonText));
        double w = dfw * 0.8 + 8 + dfw * 0.5 + ft.Width + dfw;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// QGCViewMessage  (#149 — inline status/message banner inside a page view)
// Renders a colored rounded rectangle with a message text.  Level controls
// the background color: Info=blue, Warning=orange, Error=red.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class QGCViewMessage : Control
{
    public enum MessageLevel { Info, Warning, Error }

    public static readonly StyledProperty<string>       MessageTextProperty =
        AvaloniaProperty.Register<QGCViewMessage, string>(nameof(MessageText), string.Empty);
    public static readonly StyledProperty<MessageLevel> LevelProperty =
        AvaloniaProperty.Register<QGCViewMessage, MessageLevel>(nameof(Level), MessageLevel.Info);
    public static readonly StyledProperty<bool>         IsVisibleMessageProperty =
        AvaloniaProperty.Register<QGCViewMessage, bool>(nameof(IsVisibleMessage), true);

    static QGCViewMessage()
    {
        AffectsRender<QGCViewMessage>(MessageTextProperty, LevelProperty, IsVisibleMessageProperty);
    }

    public string       MessageText      { get => GetValue(MessageTextProperty);      set => SetValue(MessageTextProperty, value); }
    public MessageLevel Level            { get => GetValue(LevelProperty);            set => SetValue(LevelProperty, value); }
    public bool         IsVisibleMessage { get => GetValue(IsVisibleMessageProperty); set => SetValue(IsVisibleMessageProperty, value); }

    private Color BgColor() => Level switch
    {
        MessageLevel.Warning => Color.FromArgb(204, QgcColors.ColorOrange.R, QgcColors.ColorOrange.G, QgcColors.ColorOrange.B),
        MessageLevel.Error   => Color.FromArgb(204, QgcColors.ColorRed.R,    QgcColors.ColorRed.G,    QgcColors.ColorRed.B),
        _                    => Color.FromArgb(204, QgcColors.ColorBlue.R,   QgcColors.ColorBlue.G,   QgcColors.ColorBlue.B),
    };

    public override void Render(DrawingContext ctx)
    {
        if (!IsVisibleMessage || string.IsNullOrEmpty(MessageText)) return;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);
        ctx.DrawRectangle(new SolidColorBrush(BgColor()), null, bounds, 4, 4);
        var ft = new FormattedText(MessageText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
            new SolidColorBrush(Colors.White));
        ctx.DrawText(ft, new Point(ScreenMetrics.DefaultFontPixelWidth, (bounds.Height - ft.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        if (!IsVisibleMessage || string.IsNullOrEmpty(MessageText))
            return new Size(0, 0);
        var ft = new FormattedText(MessageText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
            new SolidColorBrush(Colors.White));
        return new Size(ft.Width + dfw * 2, dfh * 1.6);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// StatusDot  (#158 — small colored circle with optional text label)
// Ubiquitous status indicator: green=OK, orange=warning, red=error.
// When a label is provided it is drawn to the right of the dot.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class StatusDot : Control
{
    public static readonly StyledProperty<Color>  DotColorProperty =
        AvaloniaProperty.Register<StatusDot, Color>(nameof(DotColor), QgcColors.ColorGreen);
    public static readonly StyledProperty<string> DotLabelProperty =
        AvaloniaProperty.Register<StatusDot, string>(nameof(DotLabel), string.Empty);
    public static readonly StyledProperty<double> DotRadiusProperty =
        AvaloniaProperty.Register<StatusDot, double>(nameof(DotRadius), 5.0);

    static StatusDot()
    {
        AffectsRender<StatusDot>(DotColorProperty, DotLabelProperty, DotRadiusProperty);
    }

    public Color  DotColor  { get => GetValue(DotColorProperty);  set => SetValue(DotColorProperty, value); }
    public string DotLabel  { get => GetValue(DotLabelProperty);  set => SetValue(DotLabelProperty, value); }
    public double DotRadius { get => GetValue(DotRadiusProperty); set => SetValue(DotRadiusProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var h  = Bounds.Height;
        var r  = DotRadius;
        var cx = r;
        var cy = h / 2;
        ctx.DrawEllipse(new SolidColorBrush(DotColor), null, new Point(cx, cy), r, r);

        if (!string.IsNullOrEmpty(DotLabel))
        {
            var dfh = ScreenMetrics.DefaultFontPixelHeight;
            var ft  = new FormattedText(DotLabel, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(QgcColors.Text));
            ctx.DrawText(ft, new Point(r * 2 + 3, (h - ft.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var r   = DotRadius;
        double w = r * 2;
        if (!string.IsNullOrEmpty(DotLabel))
        {
            var ft = new FormattedText(DotLabel, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9, new SolidColorBrush(QgcColors.Text));
            w += 3 + ft.Width;
        }
        return new Size(w, Math.Max(r * 2, dfh));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ProgressRing  (#159 — circular progress / loading spinner)
// Determinate: arc from top sweeping RingProgress * 360°.
// Indeterminate (IsSpinning=true): rotating arc ~270° wide using DispatcherTimer.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ProgressRing : Control
{
    public static readonly StyledProperty<double> RingProgressProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(RingProgress), 0.0);
    public static readonly StyledProperty<bool>   IsSpinningProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsSpinning), true);
    public static readonly StyledProperty<Color>  RingColorProperty =
        AvaloniaProperty.Register<ProgressRing, Color>(nameof(RingColor), QgcColors.ColorBlue);
    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(RingThickness), 3.0);

    static ProgressRing()
    {
        AffectsRender<ProgressRing>(RingProgressProperty, IsSpinningProperty, RingColorProperty, RingThicknessProperty);
    }

    public double RingProgress  { get => GetValue(RingProgressProperty);  set => SetValue(RingProgressProperty, Math.Clamp(value, 0, 1)); }
    public bool   IsSpinning    { get => GetValue(IsSpinningProperty);    set { SetValue(IsSpinningProperty, value); UpdateSpinTimer(); } }
    public Color  RingColor     { get => GetValue(RingColorProperty);     set => SetValue(RingColorProperty, value); }
    public double RingThickness { get => GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    private DispatcherTimer? _timer;
    private double _spinAngle;
    private const double ArcSpan = 270.0; // degrees of arc for indeterminate

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSpinTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    private void UpdateSpinTimer()
    {
        if (IsSpinning && _timer == null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) => { _spinAngle = (_spinAngle + 4.0) % 360.0; InvalidateVisual(); };
            _timer.Start();
        }
        else if (!IsSpinning)
        {
            _timer?.Stop();
            _timer = null;
        }
    }

    public override void Render(DrawingContext ctx)
    {
        var w   = Bounds.Width;
        var h   = Bounds.Height;
        var t   = RingThickness;
        var r   = Math.Min(w, h) / 2.0 - t / 2.0;
        var cx  = w / 2;
        var cy  = h / 2;
        var pen = new Pen(new SolidColorBrush(RingColor), t) { LineCap = PenLineCap.Round };

        // Track ring (grey)
        ctx.DrawEllipse(null, new Pen(new SolidColorBrush(QgcColors.Button), t), new Point(cx, cy), r, r);

        // Progress arc
        double startDeg, sweepDeg;
        if (IsSpinning)
        {
            startDeg = _spinAngle;
            sweepDeg = ArcSpan;
        }
        else
        {
            startDeg = -90.0; // start at top
            sweepDeg = RingProgress * 360.0;
        }
        if (sweepDeg < 1) return;

        DrawArc(ctx, pen, cx, cy, r, startDeg, sweepDeg);
    }

    private static void DrawArc(DrawingContext ctx, Pen pen, double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        if (sweepDeg >= 359.9)
        {
            ctx.DrawEllipse(null, pen, new Point(cx, cy), r, r);
            return;
        }
        double startRad = startDeg * Math.PI / 180.0;
        double endRad   = (startDeg + sweepDeg) * Math.PI / 180.0;
        var    startPt  = new Point(cx + r * Math.Sin(startRad), cy - r * Math.Cos(startRad));
        var    endPt    = new Point(cx + r * Math.Sin(endRad),   cy - r * Math.Cos(endRad));
        bool   isLarge  = sweepDeg > 180.0;

        var geo = new StreamGeometry();
        using (var sgc = geo.Open())
        {
            sgc.BeginFigure(startPt, false);
            sgc.ArcTo(endPt, new Size(r, r), 0, isLarge, SweepDirection.Clockwise);
            sgc.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, geo);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double sz = double.IsInfinity(availableSize.Width) ? 32 : availableSize.Width;
        return new Size(sz, sz);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CriticalVehicleMessageBar  (#160 — red banner listing critical vehicle messages)
// Displays up to MaxMessages recent critical messages in a scrollable strip.
// Height auto-adjusts to message count (0 when no messages → collapses).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class CriticalVehicleMessageBar : Control
{
    public static readonly StyledProperty<System.Collections.Generic.IReadOnlyList<string>?> CriticalMessagesProperty =
        AvaloniaProperty.Register<CriticalVehicleMessageBar, System.Collections.Generic.IReadOnlyList<string>?>(
            nameof(CriticalMessages));
    public static readonly StyledProperty<int> MaxMessagesProperty =
        AvaloniaProperty.Register<CriticalVehicleMessageBar, int>(nameof(MaxMessages), 3);

    static CriticalVehicleMessageBar()
    {
        AffectsRender<CriticalVehicleMessageBar>(CriticalMessagesProperty, MaxMessagesProperty);
    }

    public System.Collections.Generic.IReadOnlyList<string>? CriticalMessages
    {
        get => GetValue(CriticalMessagesProperty);
        set => SetValue(CriticalMessagesProperty, value);
    }
    public int MaxMessages { get => GetValue(MaxMessagesProperty); set => SetValue(MaxMessagesProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var msgs = CriticalMessages;
        if (msgs == null || msgs.Count == 0) return;

        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var dfw    = ScreenMetrics.DefaultFontPixelWidth;
        var bounds = new Rect(Bounds.Size);
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.AlertBackground), null, bounds);

        int count = Math.Min(msgs.Count, MaxMessages);
        double rowH = dfh * 1.3;
        for (int i = 0; i < count; i++)
        {
            var ft = new FormattedText(msgs[msgs.Count - 1 - i], CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(QgcColors.AlertText));
            ctx.DrawText(ft, new Point(dfw, i * rowH + (rowH - ft.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var msgs = CriticalMessages;
        if (msgs == null || msgs.Count == 0) return new Size(0, 0);
        var dfh   = ScreenMetrics.DefaultFontPixelHeight;
        int count = Math.Min(msgs.Count, MaxMessages);
        double w  = double.IsInfinity(availableSize.Width) ? ScreenMetrics.DefaultFontPixelWidth * 30 : availableSize.Width;
        return new Size(w, count * dfh * 1.3);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// QGCDropArea  (#166 — file drag-and-drop zone)
// Draws a dashed rectangle with a hint label.  On DragOver the border turns
// blue; DataDropped fires with the raw IDataObject on Drop.
// Callers extract files via e.Data.GetFiles() (Avalonia.Platform.Storage).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class QGCDropArea : Control
{
    public static readonly StyledProperty<string> DropHintTextProperty =
        AvaloniaProperty.Register<QGCDropArea, string>(nameof(DropHintText), "Drop file here");
    public static readonly StyledProperty<bool>   IsDragActiveProperty =
        AvaloniaProperty.Register<QGCDropArea, bool>(nameof(IsDragActive), false);

    static QGCDropArea()
    {
        AffectsRender<QGCDropArea>(DropHintTextProperty, IsDragActiveProperty);
    }

    public string DropHintText  { get => GetValue(DropHintTextProperty);  set => SetValue(DropHintTextProperty, value); }
    public bool   IsDragActive  { get => GetValue(IsDragActiveProperty);  set => SetValue(IsDragActiveProperty, value); }

    public event EventHandler<DragEventArgs>? DataDropped;

    public QGCDropArea()
    {
        Avalonia.Input.DragDrop.SetAllowDrop(this, true);
        AddHandler(Avalonia.Input.DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(Avalonia.Input.DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(Avalonia.Input.DragDrop.DropEvent,      OnDrop);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        IsDragActive = true;
        e.DragEffects = DragDropEffects.Copy;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        IsDragActive = false;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        IsDragActive = false;
        DataDropped?.Invoke(this, e);
        e.Handled = true;
    }

    public override void Render(DrawingContext ctx)
    {
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);
        var borderColor = IsDragActive ? QgcColors.ColorBlue : QgcColors.ColorGrey;

        // Dashed border
        ctx.DrawRectangle(
            IsDragActive ? new SolidColorBrush(Color.FromArgb(25, QgcColors.ColorBlue.R, QgcColors.ColorBlue.G, QgcColors.ColorBlue.B)) : null,
            new Pen(new SolidColorBrush(borderColor), 1.5) { DashStyle = DashStyle.Dash },
            bounds.Deflate(1), ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

        // Hint label centered
        var ft = new FormattedText(DropHintText, CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
            new SolidColorBrush(borderColor));
        ctx.DrawText(ft, new Point((bounds.Width - ft.Width) / 2, (bounds.Height - ft.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = double.IsInfinity(availableSize.Width)  ? dfw * 20 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? dfh * 6  : availableSize.Height;
        return new Size(w, h);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BusyIndicator  (#167 — spinner + message overlay)
// When IsActive=true covers its layout slot with a semi-transparent background,
// an animated ProgressRing, and a BusyMessage string.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class BusyIndicator : Control
{
    public static readonly StyledProperty<bool>   BusyIsActiveProperty =
        AvaloniaProperty.Register<BusyIndicator, bool>(nameof(IsActive), false);
    public static readonly StyledProperty<string> BusyMessageProperty =
        AvaloniaProperty.Register<BusyIndicator, string>(nameof(BusyMessage), "Loading…");

    static BusyIndicator()
    {
        AffectsRender<BusyIndicator>(BusyIsActiveProperty, BusyMessageProperty);
    }

    public bool   IsActive    { get => GetValue(BusyIsActiveProperty); set => SetValue(BusyIsActiveProperty, value); }
    public string BusyMessage { get => GetValue(BusyMessageProperty);  set => SetValue(BusyMessageProperty, value); }

    // Spinning state managed via simple angle + DispatcherTimer
    private Avalonia.Threading.DispatcherTimer? _timer;
    private double _angle;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsActive) StartTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop(); _timer = null;
    }

    private void StartTimer()
    {
        if (_timer != null) return;
        _timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => { _angle = (_angle + 4) % 360; InvalidateVisual(); };
        _timer.Start();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BusyIsActiveProperty)
        {
            if (IsActive) StartTimer();
            else { _timer?.Stop(); _timer = null; }
        }
    }

    public override void Render(DrawingContext ctx)
    {
        if (!IsActive) return;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);

        // Semi-transparent overlay
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(178, QgcColors.Window.R, QgcColors.Window.G, QgcColors.Window.B)),
            null, bounds);

        double ringD = Math.Min(bounds.Width, bounds.Height) * 0.3;
        double cx    = bounds.Width  / 2;
        double cy    = bounds.Height / 2 - dfh;
        double r     = ringD / 2 - 2;

        // Spinning arc (270° wide)
        double startRad = _angle * Math.PI / 180.0;
        double endRad   = (_angle + 270) * Math.PI / 180.0;
        var startPt = new Point(cx + r * Math.Sin(startRad), cy - r * Math.Cos(startRad));
        var endPt   = new Point(cx + r * Math.Sin(endRad),   cy - r * Math.Cos(endRad));

        // Track
        ctx.DrawEllipse(null, new Pen(new SolidColorBrush(QgcColors.Button), 3), new Point(cx, cy), r, r);

        // Arc
        var arc = new StreamGeometry();
        using (var sgc = arc.Open())
        {
            sgc.BeginFigure(startPt, false);
            sgc.ArcTo(endPt, new Size(r, r), 0, true, SweepDirection.Clockwise);
            sgc.EndFigure(false);
        }
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(QgcColors.ColorBlue), 3) { LineCap = PenLineCap.Round }, arc);

        // Message
        if (!string.IsNullOrEmpty(BusyMessage))
        {
            var ft = new FormattedText(BusyMessage, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(QgcColors.Text));
            ctx.DrawText(ft, new Point((bounds.Width - ft.Width) / 2, cy + r + 6));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!IsActive) return new Size(0, 0);
        double sz = double.IsInfinity(availableSize.Width) ? 80 : availableSize.Width;
        return new Size(sz, sz);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// InlineConfirmBar  (#168 — compact yes/no confirmation strip)
// Renders a one-line question with Confirm and Cancel buttons.  Disappears
// (height = 0) when IsVisible=false.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class InlineConfirmBar : Control
{
    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<InlineConfirmBar, string>(nameof(ConfirmText), "Are you sure?");
    public static readonly StyledProperty<string> ConfirmButtonTextProperty =
        AvaloniaProperty.Register<InlineConfirmBar, string>(nameof(ConfirmButtonText), "Yes");
    public static readonly StyledProperty<string> CancelButtonTextProperty =
        AvaloniaProperty.Register<InlineConfirmBar, string>(nameof(CancelButtonText), "No");
    public static readonly StyledProperty<bool>   BarVisibleProperty =
        AvaloniaProperty.Register<InlineConfirmBar, bool>(nameof(BarVisible), false);

    static InlineConfirmBar()
    {
        AffectsRender<InlineConfirmBar>(ConfirmTextProperty, ConfirmButtonTextProperty,
            CancelButtonTextProperty, BarVisibleProperty);
        FocusableProperty.OverrideMetadata<InlineConfirmBar>(new StyledPropertyMetadata<bool>(true));
    }

    public string ConfirmText       { get => GetValue(ConfirmTextProperty);       set => SetValue(ConfirmTextProperty, value); }
    public string ConfirmButtonText { get => GetValue(ConfirmButtonTextProperty); set => SetValue(ConfirmButtonTextProperty, value); }
    public string CancelButtonText  { get => GetValue(CancelButtonTextProperty);  set => SetValue(CancelButtonTextProperty, value); }
    public bool   BarVisible        { get => GetValue(BarVisibleProperty);        set => SetValue(BarVisibleProperty, value); }

    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    // Layout zones (computed in Render, used in OnPointerPressed)
    private Rect _confirmRect;
    private Rect _cancelRect;

    public override void Render(DrawingContext ctx)
    {
        if (!BarVisible) return;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var dfw    = ScreenMetrics.DefaultFontPixelWidth;
        var bounds = new Rect(Bounds.Size);
        var h      = bounds.Height;
        double btnW = dfw * 5;
        double btnH = h - 4;
        double btnY = 2;
        double r    = ScreenMetrics.DefaultBorderRadius;

        // Background
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, bounds);

        // Question text
        var ft = new FormattedText(ConfirmText, CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
            new SolidColorBrush(QgcColors.Text));
        ctx.DrawText(ft, new Point(dfw, (h - ft.Height) / 2));

        // Confirm button (right side)
        double confirmX = bounds.Width - btnW * 2 - dfw;
        _confirmRect = new Rect(confirmX, btnY, btnW, btnH);
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.ColorGreen), null, _confirmRect, r, r);
        var ftC = new FormattedText(ConfirmButtonText, CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(Colors.White));
        ctx.DrawText(ftC, new Point(confirmX + (btnW - ftC.Width) / 2, btnY + (btnH - ftC.Height) / 2));

        // Cancel button
        double cancelX = bounds.Width - btnW - dfw * 0.5;
        _cancelRect = new Rect(cancelX, btnY, btnW, btnH);
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.Button), null, _cancelRect, r, r);
        var ftX = new FormattedText(CancelButtonText, CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(QgcColors.ButtonText));
        ctx.DrawText(ftX, new Point(cancelX + (btnW - ftX.Width) / 2, btnY + (btnH - ftX.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!BarVisible) return;
        var pos = e.GetPosition(this);
        if (_confirmRect.Contains(pos)) { Confirmed?.Invoke(this, EventArgs.Empty); BarVisible = false; }
        else if (_cancelRect.Contains(pos)) { Cancelled?.Invoke(this, EventArgs.Empty); BarVisible = false; }
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!BarVisible) return new Size(0, 0);
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = double.IsInfinity(availableSize.Width) ? ScreenMetrics.DefaultFontPixelWidth * 25 : availableSize.Width;
        return new Size(w, dfh * 1.8);
    }
}

// ── #177 ConnectionLostOverlay ────────────────────────────────────────────────
public class ConnectionLostOverlay : Control
{
    public static readonly StyledProperty<bool>   CLOIsVisibleProperty =
        AvaloniaProperty.Register<ConnectionLostOverlay, bool>("CLOIsVisible", false);
    public static readonly StyledProperty<string> CLOMessageProperty =
        AvaloniaProperty.Register<ConnectionLostOverlay, string>("CLOMessage", "Connection Lost");

    public bool   CLOIsVisible { get => GetValue(CLOIsVisibleProperty); set => SetValue(CLOIsVisibleProperty, value); }
    public string CLOMessage   { get => GetValue(CLOMessageProperty);   set => SetValue(CLOMessageProperty, value); }

    static ConnectionLostOverlay()
    {
        AffectsRender<ConnectionLostOverlay>(CLOIsVisibleProperty, CLOMessageProperty);
    }

    public override void Render(DrawingContext dc)
    {
        if (!CLOIsVisible) return;
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // Semi-transparent alert background
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(200, 60, 0, 0)), new Rect(0, 0, w, h));

        // Alert icon (red circle X)
        double iconR = dfh * 1.2;
        double cx    = w / 2;
        double cy    = h * 0.35;
        dc.DrawEllipse(new SolidColorBrush(QgcColors.ColorRed), null, new Point(cx, cy), iconR, iconR);
        var pen = new Pen(new SolidColorBrush(Colors.White), 2.5);
        dc.DrawLine(pen, new Point(cx - iconR * 0.5, cy - iconR * 0.5), new Point(cx + iconR * 0.5, cy + iconR * 0.5));
        dc.DrawLine(pen, new Point(cx + iconR * 0.5, cy - iconR * 0.5), new Point(cx - iconR * 0.5, cy + iconR * 0.5));

        // Message text
        var ft = new FormattedText(CLOMessage, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 1.1, new SolidColorBrush(Colors.White));
        dc.DrawText(ft, new Point((w - ft.Width) / 2, h * 0.55));

        // Sub-text
        var sub = new FormattedText("Attempting to reconnect…",
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(sub, new Point((w - sub.Width) / 2, h * 0.68));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!CLOIsVisible) return new Size(0, 0);
        double w = !double.IsInfinity(availableSize.Width)  ? availableSize.Width  : 320;
        double h = !double.IsInfinity(availableSize.Height) ? availableSize.Height : 200;
        return new Size(w, h);
    }
}

// ── #178 OfflineMapDownloadProgress ──────────────────────────────────────────
public class OfflineMapDownloadProgress : Control
{
    public static readonly StyledProperty<double> OMDProgressProperty =
        AvaloniaProperty.Register<OfflineMapDownloadProgress, double>("OMDProgress", 0.0);
    public static readonly StyledProperty<string> OMDRegionNameProperty =
        AvaloniaProperty.Register<OfflineMapDownloadProgress, string>("OMDRegionName", string.Empty);
    public static readonly StyledProperty<long>   OMDTilesDownloadedProperty =
        AvaloniaProperty.Register<OfflineMapDownloadProgress, long>("OMDTilesDownloaded", 0);
    public static readonly StyledProperty<long>   OMDTotalTilesProperty =
        AvaloniaProperty.Register<OfflineMapDownloadProgress, long>("OMDTotalTiles", 0);
    public static readonly StyledProperty<bool>   OMDIsActiveProperty =
        AvaloniaProperty.Register<OfflineMapDownloadProgress, bool>("OMDIsActive", false);

    public double OMDProgress        { get => GetValue(OMDProgressProperty);        set => SetValue(OMDProgressProperty, value); }
    public string OMDRegionName      { get => GetValue(OMDRegionNameProperty);      set => SetValue(OMDRegionNameProperty, value); }
    public long   OMDTilesDownloaded { get => GetValue(OMDTilesDownloadedProperty); set => SetValue(OMDTilesDownloadedProperty, value); }
    public long   OMDTotalTiles      { get => GetValue(OMDTotalTilesProperty);      set => SetValue(OMDTotalTilesProperty, value); }
    public bool   OMDIsActive        { get => GetValue(OMDIsActiveProperty);        set => SetValue(OMDIsActiveProperty, value); }

    public event EventHandler? CancelRequested;

    static OfflineMapDownloadProgress()
    {
        AffectsRender<OfflineMapDownloadProgress>(OMDProgressProperty, OMDRegionNameProperty,
            OMDTilesDownloadedProperty, OMDTotalTilesProperty, OMDIsActiveProperty);
    }

    private Rect _cancelRect;

    public override void Render(DrawingContext dc)
    {
        if (!OMDIsActive) return;
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        dc.DrawRectangle(new SolidColorBrush(QgcColors.Window),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder)),
            new Rect(0, 0, w, h), br);

        // Region name
        var nameFt = new FormattedText(
            string.IsNullOrEmpty(OMDRegionName) ? "Downloading…" : OMDRegionName,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(nameFt, new Point(8, dfh * 0.2));

        // Progress bar track
        double barH   = dfh * 0.9;
        double barY   = dfh * 1.3;
        double barW   = w - 16;
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
            new Rect(8, barY, barW, barH), barH / 2);

        double fillW = barW * Math.Clamp(OMDProgress, 0, 1);
        if (fillW > 0)
            dc.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
                new Rect(8, barY, fillW, barH), barH / 2);

        // Tile count label
        string tileLabel = $"{OMDTilesDownloaded:N0} / {OMDTotalTiles:N0} tiles";
        var tileFt = new FormattedText(tileLabel, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(tileFt, new Point(8, barY + barH + 2));

        // Cancel button
        double btnH = dfh * 1.3;
        double btnW = dfh * 3.5;
        double btnX = w - btnW - 8;
        double btnY = barY + barH + dfh * 0.5;
        _cancelRect = new Rect(btnX, btnY, btnW, btnH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Button),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder)), _cancelRect, br);
        var cancelFt = new FormattedText("Cancel", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8,
            new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(cancelFt, new Point(btnX + (btnW - cancelFt.Width) / 2, btnY + (btnH - cancelFt.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_cancelRect.Contains(e.GetPosition(this)))
            CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!OMDIsActive) return new Size(0, 0);
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 280;
        return new Size(w, dfh * 4.5);
    }
}

// ── #187 QGCTabBar ────────────────────────────────────────────────────────────
public class QGCTabBar : Control
{
    public static readonly StyledProperty<IReadOnlyList<string>?> TabLabelsProperty =
        AvaloniaProperty.Register<QGCTabBar, IReadOnlyList<string>?>("TabLabels");
    public static readonly StyledProperty<int> QTBSelectedIndexProperty =
        AvaloniaProperty.Register<QGCTabBar, int>("QTBSelectedIndex", 0);

    public IReadOnlyList<string>? TabLabels         { get => GetValue(TabLabelsProperty);         set => SetValue(TabLabelsProperty, value); }
    public int                    QTBSelectedIndex  { get => GetValue(QTBSelectedIndexProperty);  set => SetValue(QTBSelectedIndexProperty, value); }

    public event EventHandler<int>? TabSelected;

    static QGCTabBar()
    {
        AffectsRender<QGCTabBar>(TabLabelsProperty, QTBSelectedIndexProperty);
    }

    private readonly System.Collections.Generic.List<Rect> _tabRects = new();

    public override void Render(DrawingContext dc)
    {
        var bounds  = Bounds;
        double w    = bounds.Width;
        double h    = bounds.Height;
        var dfh     = ScreenMetrics.DefaultFontPixelHeight;

        // Bottom border
        dc.FillRectangle(new SolidColorBrush(QgcColors.GroupBorder), new Rect(0, h - 1, w, 1));

        var labels = TabLabels;
        if (labels == null || labels.Count == 0) return;

        _tabRects.Clear();
        double tabW = w / labels.Count;

        for (int i = 0; i < labels.Count; i++)
        {
            bool active = i == QTBSelectedIndex;
            double x    = i * tabW;
            var tabRect = new Rect(x, 0, tabW, h);
            _tabRects.Add(tabRect);

            // Hover/active background
            if (active)
                dc.FillRectangle(new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), tabRect);

            // Label
            var ft = new FormattedText(labels[i], System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
                new SolidColorBrush(active ? QgcColors.Text : QgcColors.TextSecondary));
            dc.DrawText(ft, new Point(x + (tabW - ft.Width) / 2, (h - ft.Height) / 2));

            // Active underline
            if (active)
                dc.FillRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill),
                    new Rect(x + tabW * 0.1, h - 3, tabW * 0.8, 3));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        for (int i = 0; i < _tabRects.Count; i++)
        {
            if (_tabRects[i].Contains(pos))
            {
                QTBSelectedIndex = i;
                TabSelected?.Invoke(this, i);
                e.Handled = true;
                break;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 320;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 2.0);
    }
}

// ── #188 PreFlightCheckItemRow ────────────────────────────────────────────────
public enum PreFlightItemState { Unknown, Passing, Warning, Failed }

public class PreFlightCheckItemRow : Control
{
    public static readonly StyledProperty<string>          PFCLabelProperty =
        AvaloniaProperty.Register<PreFlightCheckItemRow, string>("PFCLabel", string.Empty);
    public static readonly StyledProperty<string>          PFCDescriptionProperty =
        AvaloniaProperty.Register<PreFlightCheckItemRow, string>("PFCDescription", string.Empty);
    public static readonly StyledProperty<PreFlightItemState> PFCStateProperty =
        AvaloniaProperty.Register<PreFlightCheckItemRow, PreFlightItemState>("PFCState", PreFlightItemState.Unknown);

    public string             PFCLabel       { get => GetValue(PFCLabelProperty);       set => SetValue(PFCLabelProperty, value); }
    public string             PFCDescription { get => GetValue(PFCDescriptionProperty); set => SetValue(PFCDescriptionProperty, value); }
    public PreFlightItemState PFCState       { get => GetValue(PFCStateProperty);       set => SetValue(PFCStateProperty, value); }

    static PreFlightCheckItemRow()
    {
        AffectsRender<PreFlightCheckItemRow>(PFCLabelProperty, PFCDescriptionProperty, PFCStateProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // Row separator
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // State dot
        Color dotColor = PFCState switch
        {
            PreFlightItemState.Passing => QgcColors.ColorGreen,
            PreFlightItemState.Warning => QgcColors.ColorOrange,
            PreFlightItemState.Failed  => QgcColors.ColorRed,
            _                          => QgcColors.ColorGrey
        };
        double dotR = h * 0.25;
        double dotX = h * 0.45;
        double dotY = h / 2;
        dc.DrawEllipse(new SolidColorBrush(dotColor), null, new Point(dotX, dotY), dotR, dotR);

        // Label
        double labelX = h * 0.95;
        var labelFt = new FormattedText(PFCLabel, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 0.85, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(labelFt, new Point(labelX, (h - labelFt.Height) / 2));

        // Description
        if (!string.IsNullOrEmpty(PFCDescription))
        {
            var descFt = new FormattedText(PFCDescription, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
                new SolidColorBrush(QgcColors.TextSecondary));
            double descX = labelX + labelFt.Width + dfh;
            if (descX + descFt.Width < w - 4)
                dc.DrawText(descFt, new Point(descX, (h - descFt.Height) / 2));
            else
                dc.DrawText(descFt, new Point(w - descFt.Width - 4, (h - descFt.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 300;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 1.9);
    }
}

// ── #189 VehicleMessageRow ────────────────────────────────────────────────────
public class VehicleMessageRow : Control
{
    public static readonly StyledProperty<int>    VMSeverityProperty =
        AvaloniaProperty.Register<VehicleMessageRow, int>("VMSeverity", 6); // 6=info
    public static readonly StyledProperty<string> VMTextProperty =
        AvaloniaProperty.Register<VehicleMessageRow, string>("VMText", string.Empty);
    public static readonly StyledProperty<string> VMTimestampProperty =
        AvaloniaProperty.Register<VehicleMessageRow, string>("VMTimestamp", string.Empty);
    public static readonly StyledProperty<bool>   VMIsSelectedProperty =
        AvaloniaProperty.Register<VehicleMessageRow, bool>("VMIsSelected", false);

    public int    VMSeverity    { get => GetValue(VMSeverityProperty);    set => SetValue(VMSeverityProperty, value); }
    public string VMText        { get => GetValue(VMTextProperty);        set => SetValue(VMTextProperty, value); }
    public string VMTimestamp   { get => GetValue(VMTimestampProperty);   set => SetValue(VMTimestampProperty, value); }
    public bool   VMIsSelected  { get => GetValue(VMIsSelectedProperty);  set => SetValue(VMIsSelectedProperty, value); }

    static VehicleMessageRow()
    {
        AffectsRender<VehicleMessageRow>(VMSeverityProperty, VMTextProperty, VMTimestampProperty, VMIsSelectedProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // Selection / hover background
        if (VMIsSelected)
            dc.FillRectangle(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), new Rect(0, 0, w, h));

        // Severity side bar — MAVLink severity: 0=Emergency…6=Info…7=Debug
        Color sevColor = VMSeverity <= 3 ? QgcColors.ColorRed
                       : VMSeverity == 4 ? QgcColors.ColorOrange
                       : VMSeverity == 5 ? QgcColors.ColorOrange
                       : QgcColors.ColorGrey;
        dc.FillRectangle(new SolidColorBrush(sevColor), new Rect(0, 0, 3, h));

        // Timestamp
        double tsW = 0;
        if (!string.IsNullOrEmpty(VMTimestamp))
        {
            var tsFt = new FormattedText(VMTimestamp, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(tsFt, new Point(7, (h - tsFt.Height) / 2));
            tsW = tsFt.Width + 6;
        }

        // Message text
        Color msgColor = VMSeverity <= 3 ? QgcColors.ColorRed
                       : VMSeverity <= 5 ? QgcColors.ColorOrange
                       : QgcColors.Text;
        var msgFt = new FormattedText(VMText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.83,
            new SolidColorBrush(msgColor));
        dc.DrawText(msgFt, new Point(7 + tsW, (h - msgFt.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 320;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 1.6);
    }
}

// ── #201 QGCViewPanel ─────────────────────────────────────────────────────────
public class QGCViewPanel : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<string> ViewPanelTitleProperty =
        AvaloniaProperty.Register<QGCViewPanel, string>(nameof(ViewPanelTitle), string.Empty);
    public static readonly StyledProperty<Color>  ViewPanelTitleColorProperty =
        AvaloniaProperty.Register<QGCViewPanel, Color>(nameof(ViewPanelTitleColor), Colors.Transparent);

    public string ViewPanelTitle      { get => GetValue(ViewPanelTitleProperty);      set => SetValue(ViewPanelTitleProperty, value); }
    public Color  ViewPanelTitleColor { get => GetValue(ViewPanelTitleColorProperty); set => SetValue(ViewPanelTitleColorProperty, value); }
}

// ── #202 HealthSubsystemRow ───────────────────────────────────────────────────
public class HealthSubsystemRow : Control
{
    public static readonly StyledProperty<string> HSNameProperty =
        AvaloniaProperty.Register<HealthSubsystemRow, string>("HSName", string.Empty);
    public static readonly StyledProperty<int>    HSErrorsProperty =
        AvaloniaProperty.Register<HealthSubsystemRow, int>("HSErrors", 0);
    public static readonly StyledProperty<int>    HSWarningsProperty =
        AvaloniaProperty.Register<HealthSubsystemRow, int>("HSWarnings", 0);
    public static readonly StyledProperty<bool>   HSIsOkProperty =
        AvaloniaProperty.Register<HealthSubsystemRow, bool>("HSIsOk", true);

    public string HSName     { get => GetValue(HSNameProperty);     set => SetValue(HSNameProperty, value); }
    public int    HSErrors   { get => GetValue(HSErrorsProperty);   set => SetValue(HSErrorsProperty, value); }
    public int    HSWarnings { get => GetValue(HSWarningsProperty); set => SetValue(HSWarningsProperty, value); }
    public bool   HSIsOk     { get => GetValue(HSIsOkProperty);     set => SetValue(HSIsOkProperty, value); }

    static HealthSubsystemRow()
    {
        AffectsRender<HealthSubsystemRow>(HSNameProperty, HSErrorsProperty, HSWarningsProperty, HSIsOkProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Status icon (circle)
        bool ok    = HSIsOk && HSErrors == 0;
        bool warn  = !ok && HSErrors == 0 && HSWarnings > 0;
        Color iconColor = ok ? QgcColors.ColorGreen
                        : warn ? QgcColors.ColorOrange
                               : QgcColors.ColorRed;
        double iconR = h * 0.28;
        dc.DrawEllipse(new SolidColorBrush(iconColor), null,
            new Point(iconR + 4, h / 2), iconR, iconR);

        // Subsystem name
        var nameFt = new FormattedText(HSName, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(nameFt, new Point(iconR * 2 + 10, (h - nameFt.Height) / 2));

        // Error / warning counts (right edge)
        if (HSErrors > 0 || HSWarnings > 0)
        {
            string countStr = HSErrors > 0
                ? $"{HSErrors} err{(HSErrors > 1 ? "s" : "")}"
                : $"{HSWarnings} warn{(HSWarnings > 1 ? "s" : "")}";
            Color countColor = HSErrors > 0 ? QgcColors.ColorRed : QgcColors.ColorOrange;
            var cntFt = new FormattedText(countStr, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
                new SolidColorBrush(countColor));
            dc.DrawText(cntFt, new Point(w - cntFt.Width - 6, (h - cntFt.Height) / 2));
        }
        else if (ok)
        {
            var okFt = new FormattedText("OK", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.ColorGreen));
            dc.DrawText(okFt, new Point(w - okFt.Width - 6, (h - okFt.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 280;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 1.8);
    }
}

// ── #206 QGCPopupDialog ───────────────────────────────────────────────────────
public class QGCPopupDialog : Control
{
    public static readonly StyledProperty<bool>   QDIsOpenProperty =
        AvaloniaProperty.Register<QGCPopupDialog, bool>("QDIsOpen", false);
    public static readonly StyledProperty<string> QDTitleProperty =
        AvaloniaProperty.Register<QGCPopupDialog, string>("QDTitle", string.Empty);
    public static readonly StyledProperty<double> QDPanelWidthRatioProperty =
        AvaloniaProperty.Register<QGCPopupDialog, double>("QDPanelWidthRatio", 0.7);

    public bool   QDIsOpen          { get => GetValue(QDIsOpenProperty);          set => SetValue(QDIsOpenProperty, value); }
    public string QDTitle           { get => GetValue(QDTitleProperty);           set => SetValue(QDTitleProperty, value); }
    public double QDPanelWidthRatio { get => GetValue(QDPanelWidthRatioProperty); set => SetValue(QDPanelWidthRatioProperty, value); }

    public event EventHandler? CloseRequested;

    static QGCPopupDialog()
    {
        AffectsRender<QGCPopupDialog>(QDIsOpenProperty, QDTitleProperty, QDPanelWidthRatioProperty);
    }

    private Rect _closeRect;

    public override void Render(DrawingContext dc)
    {
        if (!QDIsOpen) return;
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        // Dim overlay
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), new Rect(0, 0, w, h));

        // Dialog panel
        double panelW = w * Math.Clamp(QDPanelWidthRatio, 0.3, 0.95);
        double panelH = h * 0.8;
        double panelX = (w - panelW) / 2;
        double panelY = (h - panelH) / 2;
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Window),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder)),
            new Rect(panelX, panelY, panelW, panelH), br);

        // Title bar
        double titleH = dfh * 2.0;
        dc.FillRectangle(new SolidColorBrush(QgcColors.WindowShade),
            new Rect(panelX, panelY, panelW, titleH));
        var titleFt = new FormattedText(QDTitle, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 0.95, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(titleFt, new Point(panelX + 12, panelY + (titleH - titleFt.Height) / 2));

        // Close button (×)
        double closeSz = dfh * 1.2;
        double closeX  = panelX + panelW - closeSz - 8;
        double closeY  = panelY + (titleH - closeSz) / 2;
        _closeRect = new Rect(closeX, closeY, closeSz, closeSz);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), null,
            new Point(closeX + closeSz / 2, closeY + closeSz / 2), closeSz / 2, closeSz / 2);
        var xPen = new Pen(new SolidColorBrush(QgcColors.Text), 1.5);
        double pad2 = closeSz * 0.28;
        dc.DrawLine(xPen, new Point(closeX + pad2, closeY + pad2),
                         new Point(closeX + closeSz - pad2, closeY + closeSz - pad2));
        dc.DrawLine(xPen, new Point(closeX + closeSz - pad2, closeY + pad2),
                         new Point(closeX + pad2, closeY + closeSz - pad2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!QDIsOpen) return;
        var pos = e.GetPosition(this);
        if (_closeRect.Contains(pos)) { CloseRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!QDIsOpen) return new Size(0, 0);
        double w = !double.IsInfinity(availableSize.Width)  ? availableSize.Width  : 400;
        double h = !double.IsInfinity(availableSize.Height) ? availableSize.Height : 300;
        return new Size(w, h);
    }
}

// ── #207 ColoredDropButton ────────────────────────────────────────────────────
public class ColoredDropButton : Control
{
    public static readonly StyledProperty<string> CDBLabelProperty =
        AvaloniaProperty.Register<ColoredDropButton, string>("CDBLabel", string.Empty);
    public static readonly StyledProperty<Color>  CDBColorProperty =
        AvaloniaProperty.Register<ColoredDropButton, Color>("CDBColor", QgcColors.ColorGrey);
    public static readonly StyledProperty<bool>   CDBIsDropdownOpenProperty =
        AvaloniaProperty.Register<ColoredDropButton, bool>("CDBIsDropdownOpen", false);

    public string CDBLabel           { get => GetValue(CDBLabelProperty);           set => SetValue(CDBLabelProperty, value); }
    public Color  CDBColor           { get => GetValue(CDBColorProperty);           set => SetValue(CDBColorProperty, value); }
    public bool   CDBIsDropdownOpen  { get => GetValue(CDBIsDropdownOpenProperty);  set => SetValue(CDBIsDropdownOpenProperty, value); }

    public event EventHandler? DropdownToggleRequested;

    static ColoredDropButton()
    {
        AffectsRender<ColoredDropButton>(CDBLabelProperty, CDBColorProperty, CDBIsDropdownOpenProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        bool open = CDBIsDropdownOpen;
        dc.DrawRectangle(new SolidColorBrush(open ? QgcColors.ButtonFill : QgcColors.Button),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder)), new Rect(0, 0, w, h), br);

        // Color dot
        double dotR = h * 0.22;
        dc.DrawEllipse(new SolidColorBrush(CDBColor), null, new Point(h * 0.45, h / 2), dotR, dotR);

        // Label
        var ft = new FormattedText(CDBLabel, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(ft, new Point(h * 0.95, (h - ft.Height) / 2));

        // Chevron (down or up)
        double chvX = w - h * 0.7;
        double chvY = h / 2;
        double chvSz = dfh * 0.3;
        var chvPen = new Pen(new SolidColorBrush(QgcColors.ButtonText), 1.5);
        if (open)
        {
            dc.DrawLine(chvPen, new Point(chvX, chvY + chvSz), new Point(chvX + chvSz, chvY - chvSz));
            dc.DrawLine(chvPen, new Point(chvX + chvSz, chvY - chvSz), new Point(chvX + chvSz * 2, chvY + chvSz));
        }
        else
        {
            dc.DrawLine(chvPen, new Point(chvX, chvY - chvSz), new Point(chvX + chvSz, chvY + chvSz));
            dc.DrawLine(chvPen, new Point(chvX + chvSz, chvY + chvSz), new Point(chvX + chvSz * 2, chvY - chvSz));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        { DropdownToggleRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var ft = new FormattedText(
            string.IsNullOrEmpty(CDBLabel) ? " " : CDBLabel,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(Colors.White));
        double w = ft.Width + dfh * 4;
        return new Size(!double.IsInfinity(availableSize.Width) ? availableSize.Width : w,
                        ScreenMetrics.ImplicitButtonHeight);
    }
}

// ── #217 QGCToolTip ───────────────────────────────────────────────────────────
// Floating tooltip panel: semi-transparent dark background + wrapped text.
// Position via TranslateTransform on host. TTText sets message, TTMaxWidth constrains wrap.
public sealed class QGCToolTip : Control
{
    public static readonly StyledProperty<string> TTTextProperty =
        AvaloniaProperty.Register<QGCToolTip, string>("TTText", string.Empty);
    public static readonly StyledProperty<double> TTMaxWidthProperty =
        AvaloniaProperty.Register<QGCToolTip, double>("TTMaxWidth", 260.0);

    static QGCToolTip()
    {
        AffectsRender<QGCToolTip>(TTTextProperty, TTMaxWidthProperty);
        AffectsMeasure<QGCToolTip>(TTTextProperty, TTMaxWidthProperty);
    }

    public string TTText     { get => GetValue(TTTextProperty);     set => SetValue(TTTextProperty, value); }
    public double TTMaxWidth { get => GetValue(TTMaxWidthProperty); set => SetValue(TTMaxWidthProperty, value); }

    public override void Render(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(TTText)) return;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = 6;
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double br  = ScreenMetrics.DefaultBorderRadius;

        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 30, 30, 35)), null,
            new Rect(0, 0, w, h), br, br);

        var ft = new FormattedText(TTText,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.Text))
        {
            MaxTextWidth = Math.Max(w - pad * 2, 10)
        };
        dc.DrawText(ft, new Point(pad, (h - ft.Height) / 2));
    }

    protected override Size MeasureOverride(Size available)
    {
        if (string.IsNullOrEmpty(TTText)) return new Size(0, 0);
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = 6;
        double maxW = double.IsInfinity(available.Width) ? TTMaxWidth : Math.Min(available.Width, TTMaxWidth);
        var ft = new FormattedText(TTText,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.Text))
        {
            MaxTextWidth = maxW - pad * 2
        };
        return new Size(ft.Width + pad * 2, ft.Height + pad * 2);
    }
}

// ── #218 VehicleTypeIndicator ─────────────────────────────────────────────────
// Status dot (green=connected+disarmed, red=armed, grey=disconnected) + vehicle ID + type label.
public sealed class VehicleTypeIndicator : Control
{
    public static readonly StyledProperty<string> VTIVehicleTypeProperty =
        AvaloniaProperty.Register<VehicleTypeIndicator, string>("VTIVehicleType", "Unknown");
    public static readonly StyledProperty<string> VTIVehicleIdProperty =
        AvaloniaProperty.Register<VehicleTypeIndicator, string>("VTIVehicleId", "1");
    public static readonly StyledProperty<bool>   VTIIsConnectedProperty =
        AvaloniaProperty.Register<VehicleTypeIndicator, bool>("VTIIsConnected", false);
    public static readonly StyledProperty<bool>   VTIIsArmedProperty =
        AvaloniaProperty.Register<VehicleTypeIndicator, bool>("VTIIsArmed", false);

    static VehicleTypeIndicator()
    {
        AffectsRender<VehicleTypeIndicator>(VTIVehicleTypeProperty, VTIVehicleIdProperty,
                                            VTIIsConnectedProperty, VTIIsArmedProperty);
    }

    public string VTIVehicleType { get => GetValue(VTIVehicleTypeProperty); set => SetValue(VTIVehicleTypeProperty, value); }
    public string VTIVehicleId   { get => GetValue(VTIVehicleIdProperty);   set => SetValue(VTIVehicleIdProperty, value); }
    public bool   VTIIsConnected { get => GetValue(VTIIsConnectedProperty); set => SetValue(VTIIsConnectedProperty, value); }
    public bool   VTIIsArmed     { get => GetValue(VTIIsArmedProperty);     set => SetValue(VTIIsArmedProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;

        double dotR = h * 0.18;
        Color dotC  = !VTIIsConnected ? QgcColors.ColorGrey :
                       VTIIsArmed     ? QgcColors.ColorRed  : QgcColors.ColorGreen;
        dc.DrawEllipse(new SolidColorBrush(dotC), null, new Point(dotR + 3, h / 2), dotR, dotR);

        var idFt = new FormattedText($"V{VTIVehicleId}",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.Bold),
            dfh * 0.9, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(idFt, new Point(dotR * 2 + 7, h / 2 - idFt.Height));

        var typeFt = new FormattedText(VTIVehicleType,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.7, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(typeFt, new Point(dotR * 2 + 7, h / 2));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 120 : available.Width;
        double h = double.IsInfinity(available.Height) ? 40  : available.Height;
        return new Size(w, h);
    }
}

// ── #228 QGCFileDialog ────────────────────────────────────────────────────────
// File-path selection row: read-only path display box + "Browse…" button.
// QFDFilePath shows the current path; clicking the Browse button raises BrowseRequested.
// QFDLabel is an optional left label; QFDFilter is a hint shown when path is empty.
public sealed class QGCFileDialog : Control
{
    public static readonly StyledProperty<string> QFDFilePathProperty =
        AvaloniaProperty.Register<QGCFileDialog, string>("QFDFilePath", string.Empty);
    public static readonly StyledProperty<string> QFDLabelProperty =
        AvaloniaProperty.Register<QGCFileDialog, string>("QFDLabel", string.Empty);
    public static readonly StyledProperty<string> QFDFilterProperty =
        AvaloniaProperty.Register<QGCFileDialog, string>("QFDFilter", "Select a file…");

    static QGCFileDialog()
    {
        AffectsRender<QGCFileDialog>(QFDFilePathProperty, QFDLabelProperty, QFDFilterProperty);
        AffectsMeasure<QGCFileDialog>(QFDLabelProperty);
    }

    public string QFDFilePath { get => GetValue(QFDFilePathProperty); set => SetValue(QFDFilePathProperty, value); }
    public string QFDLabel    { get => GetValue(QFDLabelProperty);    set => SetValue(QFDLabelProperty, value); }
    public string QFDFilter   { get => GetValue(QFDFilterProperty);   set => SetValue(QFDFilterProperty, value); }

    public event EventHandler? BrowseRequested;

    private Rect _browseRect;

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;
        double br  = ScreenMetrics.DefaultBorderRadius;
        double btnW = dfh * 5;
        double pad  = 4;

        double labelW = 0;
        if (!string.IsNullOrEmpty(QFDLabel))
        {
            var lbFt = new FormattedText(QFDLabel,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(lbFt, new Point(pad, (h - lbFt.Height) / 2));
            labelW = lbFt.Width + dfw;
        }

        // Path box
        double pathW = w - labelW - btnW - dfw - pad;
        var pathRect = new Rect(labelW + pad, pad, pathW, h - pad * 2);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Window),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1), pathRect, br, br);

        string displayPath = string.IsNullOrEmpty(QFDFilePath) ? QFDFilter : QFDFilePath;
        Color pathColor    = string.IsNullOrEmpty(QFDFilePath) ? QgcColors.TextSecondary : QgcColors.Text;
        var pathFt = new FormattedText(displayPath,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.8, new SolidColorBrush(pathColor))
        { MaxTextWidth = pathW - pad * 2 };
        dc.DrawText(pathFt, new Point(pathRect.X + pad, (h - pathFt.Height) / 2));

        // Browse button
        _browseRect = new Rect(labelW + pad + pathW + dfw, pad, btnW, h - pad * 2);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.ButtonFill), null, _browseRect, br, br);
        var btnFt = new FormattedText("Browse…",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.78, new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(btnFt, new Point(_browseRect.X + (_browseRect.Width - btnFt.Width) / 2,
                                      (h - btnFt.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_browseRect.Width > 0 && _browseRect.Contains(e.GetPosition(this)))
        {
            BrowseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width) ? 360 : available.Width;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight + 8);
    }
}

// ── #229 ConfirmSlider ────────────────────────────────────────────────────────
// Horizontal slide-to-confirm widget.  The user drags the thumb right to 100%;
// on reaching the end it fires Confirmed.  Dragging back cancels.
// CSPromptText is shown in the track background; CSIsConfirmed reflects state.
public sealed class ConfirmSlider : Control
{
    public static readonly StyledProperty<string> CSPromptTextProperty =
        AvaloniaProperty.Register<ConfirmSlider, string>("CSPromptText", "Slide to confirm");
    public static readonly StyledProperty<Color>  CSAccentColorProperty =
        AvaloniaProperty.Register<ConfirmSlider, Color>("CSAccentColor", Color.FromRgb(220, 80, 0));
    public static readonly StyledProperty<bool>   CSIsConfirmedProperty =
        AvaloniaProperty.Register<ConfirmSlider, bool>("CSIsConfirmed", false);

    static ConfirmSlider()
    {
        AffectsRender<ConfirmSlider>(CSPromptTextProperty, CSAccentColorProperty, CSIsConfirmedProperty);
    }

    public string CSPromptText  { get => GetValue(CSPromptTextProperty);  set => SetValue(CSPromptTextProperty, value); }
    public Color  CSAccentColor { get => GetValue(CSAccentColorProperty); set => SetValue(CSAccentColorProperty, value); }
    public bool   CSIsConfirmed { get => GetValue(CSIsConfirmedProperty); set => SetValue(CSIsConfirmedProperty, value); }

    public event EventHandler? Confirmed;

    private double _thumbFrac;  // 0.0–1.0
    private bool   _dragging;
    private double _dragStartX;
    private double _thumbStartFrac;

    private const double ThumbW = 44;

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double br  = h / 2;

        // Track background
        Color trackFill = CSIsConfirmed
            ? CSAccentColor
            : Color.FromArgb(255,
                (byte)(CSAccentColor.R / 4),
                (byte)(CSAccentColor.G / 4),
                (byte)(CSAccentColor.B / 4));
        dc.DrawRectangle(new SolidColorBrush(trackFill), null, new Rect(0, 0, w, h), br, br);

        // Fill progress
        double thumbX = _thumbFrac * (w - ThumbW);
        if (thumbX > 0)
        {
            var fillGeo = new StreamGeometry();
            using (var ctx2 = fillGeo.Open())
            {
                ctx2.BeginFigure(new Point(br, 0), true);
                ctx2.LineTo(new Point(thumbX + ThumbW / 2, 0));
                ctx2.LineTo(new Point(thumbX + ThumbW / 2, h));
                ctx2.LineTo(new Point(br, h));
                ctx2.ArcTo(new Point(br, 0), new Size(br, br), 0, false, SweepDirection.CounterClockwise);
                ctx2.EndFigure(false);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(100,
                CSAccentColor.R, CSAccentColor.G, CSAccentColor.B)), null, fillGeo);
        }

        // Prompt text (centred in track)
        if (!CSIsConfirmed)
        {
            var ft = new FormattedText(CSPromptText,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.78, new SolidColorBrush(Colors.White));
            dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
        }

        // Thumb
        dc.DrawRectangle(new SolidColorBrush(CSAccentColor), null,
            new Rect(thumbX, 0, ThumbW, h), br, br);
        // Arrow chevron on thumb
        var penW = new Pen(new SolidColorBrush(Colors.White), 2);
        double cx = thumbX + ThumbW / 2;
        double cy = h / 2;
        double as2 = h * 0.22;
        dc.DrawLine(penW, new Point(cx - as2 * 0.5, cy - as2), new Point(cx + as2 * 0.5, cy));
        dc.DrawLine(penW, new Point(cx - as2 * 0.5, cy + as2), new Point(cx + as2 * 0.5, cy));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        double thumbX = _thumbFrac * (Bounds.Width - ThumbW);
        var pos = e.GetPosition(this);
        if (pos.X >= thumbX && pos.X <= thumbX + ThumbW)
        {
            _dragging = true;
            _dragStartX = pos.X;
            _thumbStartFrac = _thumbFrac;
            e.Pointer.Capture(this);
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        double w = Bounds.Width;
        double delta = e.GetPosition(this).X - _dragStartX;
        _thumbFrac = Math.Clamp(_thumbStartFrac + delta / (w - ThumbW), 0.0, 1.0);
        if (_thumbFrac >= 1.0 && !CSIsConfirmed)
        {
            CSIsConfirmed = true;
            Confirmed?.Invoke(this, EventArgs.Empty);
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        if (!CSIsConfirmed) { _thumbFrac = 0; InvalidateVisual(); }
        e.Pointer.Capture(null);
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width) ? 260 : available.Width;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight + 4);
    }
}

// ── #242 QGCSwipeView ─────────────────────────────────────────────────────────
// Horizontal tab-pill strip showing page names; the active tab has a coloured
// underline.  Clicking a tab raises TabSelected(index).
// QSVTabs is IReadOnlyList<string>; QSVActiveIndex is the current page.
public sealed class QGCSwipeView : Control
{
    public static readonly StyledProperty<IReadOnlyList<string>?> QSVTabsProperty =
        AvaloniaProperty.Register<QGCSwipeView, IReadOnlyList<string>?>("QSVTabs", null);
    public static readonly StyledProperty<int> QSVActiveIndexProperty =
        AvaloniaProperty.Register<QGCSwipeView, int>("QSVActiveIndex", 0);

    static QGCSwipeView()
    {
        AffectsRender<QGCSwipeView>(QSVTabsProperty, QSVActiveIndexProperty);
        AffectsMeasure<QGCSwipeView>(QSVTabsProperty);
    }

    public IReadOnlyList<string>? QSVTabs       { get => GetValue(QSVTabsProperty);       set => SetValue(QSVTabsProperty, value); }
    public int                    QSVActiveIndex{ get => GetValue(QSVActiveIndexProperty); set => SetValue(QSVActiveIndexProperty, value); }

    public event EventHandler<int>? TabSelected;

    private readonly System.Collections.Generic.List<Rect> _tabRects = [];

    public override void Render(DrawingContext dc)
    {
        var tabs = QSVTabs;
        if (tabs == null || tabs.Count == 0) return;

        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = dfh * 0.7;

        // Bottom divider
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        _tabRects.Clear();
        double x = 0;
        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = i == QSVActiveIndex;
            var ft = new FormattedText(tabs[i],
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                active
                    ? new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold)
                    : Typeface.Default,
                dfh * 0.85,
                new SolidColorBrush(active ? QgcColors.Text : QgcColors.TextSecondary));

            double tabW = ft.Width + pad * 2;
            var tabRect = new Rect(x, 0, tabW, h);
            _tabRects.Add(tabRect);

            // Active underline
            if (active)
                dc.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
                    new Rect(x + 2, h - 3, tabW - 4, 3), 1.5, 1.5);

            dc.DrawText(ft, new Point(x + pad, (h - ft.Height) / 2));
            x += tabW;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        for (int i = 0; i < _tabRects.Count; i++)
        {
            if (_tabRects[i].Contains(pos))
            {
                QSVActiveIndex = i;
                TabSelected?.Invoke(this, i);
                e.Handled = true;
                return;
            }
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        var tabs = QSVTabs;
        if (tabs == null || tabs.Count == 0)
            return new Size(!double.IsInfinity(available.Width) ? available.Width : 200,
                            ScreenMetrics.ImplicitButtonHeight);

        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = dfh * 0.7;
        double totalW = 0;
        foreach (var t in tabs)
        {
            var ft = new FormattedText(t,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.85, new SolidColorBrush(Colors.White));
            totalW += ft.Width + pad * 2;
        }
        double w = !double.IsInfinity(available.Width) ? available.Width : totalW;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}

// ── #243 ConnectionSettingsRow ────────────────────────────────────────────────
// Single link/connection entry row for the Settings > Links panel.
// Shows: type pill + name + address:port + remove (×) button.
// CSRLinkType ("Serial"/"UDP"/"TCP"), CSRLinkName, CSRAddress, CSRPort.
// Raises RemoveRequested on × click; EditRequested on row body click.
public sealed class ConnectionSettingsRow : Control
{
    public static readonly StyledProperty<string> CSRLinkTypeProperty =
        AvaloniaProperty.Register<ConnectionSettingsRow, string>("CSRLinkType", "UDP");
    public static readonly StyledProperty<string> CSRLinkNameProperty =
        AvaloniaProperty.Register<ConnectionSettingsRow, string>("CSRLinkName", string.Empty);
    public static readonly StyledProperty<string> CSRAddressProperty =
        AvaloniaProperty.Register<ConnectionSettingsRow, string>("CSRAddress", string.Empty);
    public static readonly StyledProperty<int>    CSRPortProperty =
        AvaloniaProperty.Register<ConnectionSettingsRow, int>("CSRPort", 14550);
    public static readonly StyledProperty<bool>   CSRIsConnectedProperty =
        AvaloniaProperty.Register<ConnectionSettingsRow, bool>("CSRIsConnected", false);

    static ConnectionSettingsRow()
    {
        AffectsRender<ConnectionSettingsRow>(CSRLinkTypeProperty, CSRLinkNameProperty,
            CSRAddressProperty, CSRPortProperty, CSRIsConnectedProperty);
    }

    public string CSRLinkType   { get => GetValue(CSRLinkTypeProperty);   set => SetValue(CSRLinkTypeProperty, value); }
    public string CSRLinkName   { get => GetValue(CSRLinkNameProperty);   set => SetValue(CSRLinkNameProperty, value); }
    public string CSRAddress    { get => GetValue(CSRAddressProperty);    set => SetValue(CSRAddressProperty, value); }
    public int    CSRPort       { get => GetValue(CSRPortProperty);       set => SetValue(CSRPortProperty, value); }
    public bool   CSRIsConnected{ get => GetValue(CSRIsConnectedProperty);set => SetValue(CSRIsConnectedProperty, value); }

    public event EventHandler? RemoveRequested;
    public event EventHandler? EditRequested;

    private Rect _removeRect;

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        double x = dfw * 0.4;

        // Type pill
        var typeFt = new FormattedText(CSRLinkType,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.7, new SolidColorBrush(QgcColors.ButtonText));
        double pillW = typeFt.Width + dfh * 0.7;
        double pillH = dfh * 1.0;
        double pillY = (h - pillH) / 2;
        dc.DrawRectangle(new SolidColorBrush(QgcColors.ColorBlue), null,
            new Rect(x, pillY, pillW, pillH), pillH / 2, pillH / 2);
        dc.DrawText(typeFt, new Point(x + (pillW - typeFt.Width) / 2, pillY + (pillH - typeFt.Height) / 2));
        x += pillW + dfw;

        // Name
        var nameFt = new FormattedText(CSRLinkName,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 0.85, new SolidColorBrush(QgcColors.Text))
        { MaxTextWidth = w * 0.3 };
        dc.DrawText(nameFt, new Point(x, (h - nameFt.Height) / 2));
        x += nameFt.Width + dfw;

        // Address:port
        string addrTxt = string.IsNullOrEmpty(CSRAddress) ? $":{CSRPort}" : $"{CSRAddress}:{CSRPort}";
        var addrFt = new FormattedText(addrTxt,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.78, new SolidColorBrush(QgcColors.TextSecondary))
        { MaxTextWidth = w * 0.35 };
        dc.DrawText(addrFt, new Point(x, (h - addrFt.Height) / 2));

        // Connected dot
        double dotR = h * 0.15;
        Color  dotC = CSRIsConnected ? QgcColors.ColorGreen : QgcColors.ColorGrey;
        dc.DrawEllipse(new SolidColorBrush(dotC), null, new Point(w - dfh * 2.2, h / 2), dotR, dotR);

        // Remove button (×)
        double remSz = dfh * 1.1;
        _removeRect = new Rect(w - remSz - dfw * 0.3, (h - remSz) / 2, remSz, remSz);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, _removeRect,
            _removeRect.Height / 2, _removeRect.Height / 2);
        var xFt = new FormattedText("×",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.9, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(xFt, new Point(_removeRect.X + (_removeRect.Width - xFt.Width) / 2,
                                   _removeRect.Y + (_removeRect.Height - xFt.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        if (_removeRect.Width > 0 && _removeRect.Contains(pos))
        {
            RemoveRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else
        {
            EditRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width) ? available.Width : 360;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight + 4);
    }
}

// ══════════════════════════════════════════════════════════════════
// #246 — QGCRoundButton
// QGC: src/QmlControls/QGCRoundButton.qml
// Circular icon button with optional spinning animation (refresh/loading).
// ══════════════════════════════════════════════════════════════════

public sealed class QGCRoundButton : Control
{
    public static readonly StyledProperty<Geometry?> QRBIconGeometryProperty =
        AvaloniaProperty.Register<QGCRoundButton, Geometry?>("QRBIconGeometry", null);
    public static readonly StyledProperty<Color> QRBButtonColorProperty =
        AvaloniaProperty.Register<QGCRoundButton, Color>("QRBButtonColor", default);
    public static readonly StyledProperty<Color> QRBIconColorProperty =
        AvaloniaProperty.Register<QGCRoundButton, Color>("QRBIconColor", default);
    public static readonly StyledProperty<bool> QRBIsSpinningProperty =
        AvaloniaProperty.Register<QGCRoundButton, bool>("QRBIsSpinning", false);
    public static readonly StyledProperty<double> QRBRadiusProperty =
        AvaloniaProperty.Register<QGCRoundButton, double>("QRBRadius", 20.0);

    public Geometry? QRBIconGeometry { get => GetValue(QRBIconGeometryProperty); set => SetValue(QRBIconGeometryProperty, value); }
    public Color     QRBButtonColor  { get => GetValue(QRBButtonColorProperty);  set => SetValue(QRBButtonColorProperty, value); }
    public Color     QRBIconColor    { get => GetValue(QRBIconColorProperty);    set => SetValue(QRBIconColorProperty, value); }
    public bool      QRBIsSpinning   { get => GetValue(QRBIsSpinningProperty);   set => SetValue(QRBIsSpinningProperty, value); }
    public double    QRBRadius       { get => GetValue(QRBRadiusProperty);       set => SetValue(QRBRadiusProperty, value); }

    public event EventHandler? Clicked;

    private bool _hovered;
    private bool _pressed;
    private double _spinAngle;
    private DispatcherTimer? _timer;

    static QGCRoundButton()
    {
        AffectsRender<QGCRoundButton>(QRBIconGeometryProperty, QRBButtonColorProperty,
            QRBIconColorProperty, QRBIsSpinningProperty, QRBRadiusProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => { if (QRBIsSpinning) { _spinAngle = (_spinAngle + 8) % 360; InvalidateVisual(); } };
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext dc)
    {
        double r   = QRBRadius;
        double cx  = Bounds.Width  / 2;
        double cy  = Bounds.Height / 2;

        Color bgC = QRBButtonColor == default ? QgcColors.Button : QRBButtonColor;
        if (_pressed)  bgC = Color.FromArgb(bgC.A, (byte)Math.Max(bgC.R - 25, 0),
                                                    (byte)Math.Max(bgC.G - 25, 0),
                                                    (byte)Math.Max(bgC.B - 25, 0));
        else if (_hovered) bgC = Color.FromArgb(bgC.A, (byte)Math.Min(bgC.R + 20, 255),
                                                        (byte)Math.Min(bgC.G + 20, 255),
                                                        (byte)Math.Min(bgC.B + 20, 255));

        dc.DrawEllipse(new SolidColorBrush(bgC),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1),
            new Point(cx, cy), r, r);

        if (QRBIconGeometry != null)
        {
            Color iconC = QRBIconColor == default ? QgcColors.ButtonText : QRBIconColor;
            using var rotate = dc.PushTransform(
                Matrix.CreateTranslation(-cx, -cy) *
                Matrix.CreateRotation(_spinAngle * Math.PI / 180.0) *
                Matrix.CreateTranslation(cx, cy));
            Rect geoBounds = QRBIconGeometry.Bounds;
            double scale = Math.Min((r * 1.2) / Math.Max(geoBounds.Width, geoBounds.Height), 1.5);
            double ox = cx - geoBounds.Width  * scale / 2 - geoBounds.X * scale;
            double oy = cy - geoBounds.Height * scale / 2 - geoBounds.Y * scale;
            using var xf = dc.PushTransform(Matrix.CreateScale(scale, scale) *
                                            Matrix.CreateTranslation(ox, oy));
            dc.DrawGeometry(new SolidColorBrush(iconC), null, QRBIconGeometry);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)   { base.OnPointerEntered(e);   _hovered = true;  InvalidateVisual(); }
    protected override void OnPointerExited(PointerEventArgs e)    { base.OnPointerExited(e);    _hovered = false; InvalidateVisual(); }
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
            Clicked?.Invoke(this, EventArgs.Empty);
        _pressed = false;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size available)
    {
        double d = QRBRadius * 2;
        return new Size(d, d);
    }
}

// ══════════════════════════════════════════════════════════════════
// #247 — KMLOrSHPFileDialog
// QGC: src/QmlControls/KMLOrSHPFileDialog.qml
// Overlay dialog for importing KML/KMZ/SHP geographic files.
// ══════════════════════════════════════════════════════════════════

public sealed class KMLOrSHPFileDialog : Control
{
    public static readonly StyledProperty<bool>   KFDIsOpenProperty =
        AvaloniaProperty.Register<KMLOrSHPFileDialog, bool>("KFDIsOpen", false);
    public static readonly StyledProperty<string> KFDFilePathProperty =
        AvaloniaProperty.Register<KMLOrSHPFileDialog, string>("KFDFilePath", "");
    public static readonly StyledProperty<string> KFDTitleProperty =
        AvaloniaProperty.Register<KMLOrSHPFileDialog, string>("KFDTitle", "Open KML or SHP File");

    public bool   KFDIsOpen   { get => GetValue(KFDIsOpenProperty);   set => SetValue(KFDIsOpenProperty, value); }
    public string KFDFilePath { get => GetValue(KFDFilePathProperty); set => SetValue(KFDFilePathProperty, value); }
    public string KFDTitle    { get => GetValue(KFDTitleProperty);    set => SetValue(KFDTitleProperty, value); }

    public event EventHandler?          BrowseRequested;
    public event EventHandler<string>?  FileAccepted;
    public event EventHandler?          Cancelled;

    private Rect _browseRect;
    private Rect _okRect;
    private Rect _cancelRect;

    static KMLOrSHPFileDialog()
    {
        AffectsRender<KMLOrSHPFileDialog>(KFDIsOpenProperty, KFDFilePathProperty, KFDTitleProperty);
    }

    public override void Render(DrawingContext dc)
    {
        if (!KFDIsOpen) return;
        double w = Bounds.Width;
        double h = Bounds.Height;

        // Overlay
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), null, new Rect(0, 0, w, h));

        // Panel
        double pw = 420;
        double ph = 210;
        double px = (w - pw) / 2;
        double py = (h - ph) / 2;
        double br = ScreenMetrics.DefaultBorderRadius;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = 16;

        dc.DrawRectangle(new SolidColorBrush(QgcColors.Window),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1),
            new Rect(px, py, pw, ph), br, br);

        // Title
        var titleFt = new FormattedText(KFDTitle, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface(Typeface.Default.FontFamily, weight: FontWeight.Bold),
            dfh, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(titleFt, new Point(px + pad, py + pad));

        // Path row
        double rowY  = py + pad + dfh + pad * 0.8;
        double pathW = pw - pad * 2 - 90;
        string display = string.IsNullOrEmpty(KFDFilePath) ? "(no file selected)" : KFDFilePath;
        var pathBg = new SolidColorBrush(QgcColors.WindowShade);
        dc.DrawRectangle(pathBg, new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1),
            new Rect(px + pad, rowY, pathW, 28), 4, 4);
        var pathFt = new FormattedText(display, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.Text));
        using var clip = dc.PushClip(new Rect(px + pad + 4, rowY, pathW - 8, 28));
        dc.DrawText(pathFt, new Point(px + pad + 4, rowY + (28 - pathFt.Height) / 2));

        _browseRect = new Rect(px + pad + pathW + 8, rowY, 74, 28);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Button),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1),
            _browseRect, 4, 4);
        var bFt = new FormattedText("Browse", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75, new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(bFt, new Point(_browseRect.X + (_browseRect.Width - bFt.Width) / 2,
                                   _browseRect.Y + (_browseRect.Height - bFt.Height) / 2));

        // Format hint
        double hintY = rowY + 36;
        var hintFt = new FormattedText("Supported formats: .kml  .kmz  .shp",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.72, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(hintFt, new Point(px + pad, hintY));

        // Buttons
        double btnW = 80;
        double btnH = ScreenMetrics.ImplicitButtonHeight;
        double btnY = py + ph - btnH - pad;

        _okRect = new Rect(px + pw - pad * 2 - btnW * 2, btnY, btnW, btnH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null, _okRect, 4, 4);
        var okFt = new FormattedText("OK", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(okFt, new Point(_okRect.X + (_okRect.Width - okFt.Width) / 2,
                                    _okRect.Y + (_okRect.Height - okFt.Height) / 2));

        _cancelRect = new Rect(px + pw - pad - btnW, btnY, btnW, btnH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Button), null, _cancelRect, 4, 4);
        var cFt = new FormattedText("Cancel", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(cFt, new Point(_cancelRect.X + (_cancelRect.Width - cFt.Width) / 2,
                                   _cancelRect.Y + (_cancelRect.Height - cFt.Height) / 2));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!KFDIsOpen) return;
        var pos = e.GetPosition(this);
        if (_browseRect.Width > 0 && _browseRect.Contains(pos))
            BrowseRequested?.Invoke(this, EventArgs.Empty);
        else if (_okRect.Width > 0 && _okRect.Contains(pos))
            FileAccepted?.Invoke(this, KFDFilePath);
        else if (_cancelRect.Width > 0 && _cancelRect.Contains(pos))
        { KFDIsOpen = false; Cancelled?.Invoke(this, EventArgs.Empty); }
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size available) =>
        KFDIsOpen ? available : new Size(0, 0);
}

// ══════════════════════════════════════════════════════════════════
// #244 — EditPositionDialog
// QGC: src/QmlControls/EditPositionDialog.qml
// Modal dialog for editing a waypoint position in Geographic/UTM/MGRS
// coordinate formats.
// ══════════════════════════════════════════════════════════════════

public enum EditPositionMode { Geographic, UTM, MGRS }

public sealed class EditPositionDialog : Control
{
    public static readonly StyledProperty<bool>             EPDIsOpenProperty =
        AvaloniaProperty.Register<EditPositionDialog, bool>("EPDIsOpen", false);
    public static readonly StyledProperty<double>           EPDInitialLatProperty =
        AvaloniaProperty.Register<EditPositionDialog, double>("EPDInitialLat", 0.0);
    public static readonly StyledProperty<double>           EPDInitialLonProperty =
        AvaloniaProperty.Register<EditPositionDialog, double>("EPDInitialLon", 0.0);
    public static readonly StyledProperty<double>           EPDInitialAltProperty =
        AvaloniaProperty.Register<EditPositionDialog, double>("EPDInitialAlt", 0.0);
    public static readonly StyledProperty<EditPositionMode> EPDModeProperty =
        AvaloniaProperty.Register<EditPositionDialog, EditPositionMode>("EPDMode", EditPositionMode.Geographic);

    public bool             EPDIsOpen     { get => GetValue(EPDIsOpenProperty);     set => SetValue(EPDIsOpenProperty, value); }
    public double           EPDInitialLat { get => GetValue(EPDInitialLatProperty); set => SetValue(EPDInitialLatProperty, value); }
    public double           EPDInitialLon { get => GetValue(EPDInitialLonProperty); set => SetValue(EPDInitialLonProperty, value); }
    public double           EPDInitialAlt { get => GetValue(EPDInitialAltProperty); set => SetValue(EPDInitialAltProperty, value); }
    public EditPositionMode EPDMode       { get => GetValue(EPDModeProperty);       set => SetValue(EPDModeProperty, value); }

    public event EventHandler<(double Lat, double Lon, double Alt)>? PositionAccepted;
    public event EventHandler?                                        Cancelled;

    // Stored field values (editable at runtime by host logic)
    public double EditLat { get; set; }
    public double EditLon { get; set; }
    public double EditAlt { get; set; }
    public string EditUtmZone     { get; set; } = "";
    public double EditUtmEasting  { get; set; }
    public double EditUtmNorthing { get; set; }
    public string EditMgrs        { get; set; } = "";

    // Hit-test rects
    private Rect _tabGeoRect;
    private Rect _tabUtmRect;
    private Rect _tabMgrsRect;
    private Rect _okRect;
    private Rect _cancelRect;

    static EditPositionDialog()
    {
        AffectsRender<EditPositionDialog>(EPDIsOpenProperty, EPDModeProperty,
            EPDInitialLatProperty, EPDInitialLonProperty, EPDInitialAltProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EPDIsOpenProperty && EPDIsOpen)
        {
            EditLat = EPDInitialLat;
            EditLon = EPDInitialLon;
            EditAlt = EPDInitialAlt;
        }
    }

    public override void Render(DrawingContext dc)
    {
        if (!EPDIsOpen) return;
        double w = Bounds.Width;
        double h = Bounds.Height;

        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), null, new Rect(0, 0, w, h));

        double pw  = 380;
        double ph  = 310;
        double px  = (w - pw) / 2;
        double py  = (h - ph) / 2;
        double br  = ScreenMetrics.DefaultBorderRadius;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = 14;

        dc.DrawRectangle(new SolidColorBrush(QgcColors.Window),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1),
            new Rect(px, py, pw, ph), br, br);

        // Title
        var titleFt = new FormattedText("Edit Position",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, weight: FontWeight.Bold),
            dfh, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(titleFt, new Point(px + pad, py + pad));

        // Tab bar
        double tabY  = py + pad + dfh + pad * 0.6;
        double tabH  = 26;
        double tabW  = (pw - pad * 2) / 3.0;
        string[] tabLabels = { "Geographic", "UTM", "MGRS" };
        EditPositionMode[] tabModes = { EditPositionMode.Geographic, EditPositionMode.UTM, EditPositionMode.MGRS };
        Rect[] tabRects = new Rect[3];
        for (int i = 0; i < 3; i++)
        {
            double tx = px + pad + i * tabW;
            tabRects[i] = new Rect(tx, tabY, tabW, tabH);
            bool active = EPDMode == tabModes[i];
            dc.DrawRectangle(new SolidColorBrush(active ? QgcColors.PrimaryButtonFill : QgcColors.WindowShade),
                new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1), tabRects[i]);
            var tabFt = new FormattedText(tabLabels[i],
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.78,
                new SolidColorBrush(active ? QgcColors.ButtonText : QgcColors.Text));
            dc.DrawText(tabFt, new Point(tx + (tabW - tabFt.Width) / 2, tabY + (tabH - tabFt.Height) / 2));
        }
        _tabGeoRect  = tabRects[0];
        _tabUtmRect  = tabRects[1];
        _tabMgrsRect = tabRects[2];

        // Fields
        double fieldY = tabY + tabH + pad * 0.8;
        double fieldH = 26;
        double labelW = 90;
        double fieldW = pw - pad * 2 - labelW - 8;
        void DrawField(string label, string value, double y)
        {
            var lFt = new FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.Text));
            dc.DrawText(lFt, new Point(px + pad, y + (fieldH - lFt.Height) / 2));
            double fx = px + pad + labelW + 8;
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade),
                new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1), new Rect(fx, y, fieldW, fieldH), 3, 3);
            var vFt = new FormattedText(value, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.Text));
            dc.DrawText(vFt, new Point(fx + 6, y + (fieldH - vFt.Height) / 2));
        }

        switch (EPDMode)
        {
            case EditPositionMode.Geographic:
                DrawField("Latitude",  $"{EditLat:F7}", fieldY);
                DrawField("Longitude", $"{EditLon:F7}", fieldY + fieldH + 8);
                DrawField("Altitude (m)", $"{EditAlt:F1}", fieldY + (fieldH + 8) * 2);
                break;
            case EditPositionMode.UTM:
                DrawField("Zone",     EditUtmZone, fieldY);
                DrawField("Easting",  $"{EditUtmEasting:F1}",  fieldY + fieldH + 8);
                DrawField("Northing", $"{EditUtmNorthing:F1}", fieldY + (fieldH + 8) * 2);
                DrawField("Altitude (m)", $"{EditAlt:F1}", fieldY + (fieldH + 8) * 3);
                break;
            case EditPositionMode.MGRS:
                DrawField("MGRS",     EditMgrs, fieldY);
                DrawField("Altitude (m)", $"{EditAlt:F1}", fieldY + fieldH + 8);
                break;
        }

        // OK / Cancel
        double btnW = 80;
        double btnH = ScreenMetrics.ImplicitButtonHeight;
        double btnY = py + ph - btnH - pad;

        _okRect = new Rect(px + pw - pad * 2 - btnW * 2, btnY, btnW, btnH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null, _okRect, 4, 4);
        var okFt = new FormattedText("OK", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(okFt, new Point(_okRect.X + (_okRect.Width - okFt.Width) / 2,
                                    _okRect.Y + (_okRect.Height - okFt.Height) / 2));

        _cancelRect = new Rect(px + pw - pad - btnW, btnY, btnW, btnH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Button), null, _cancelRect, 4, 4);
        var cFt = new FormattedText("Cancel", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(cFt, new Point(_cancelRect.X + (_cancelRect.Width - cFt.Width) / 2,
                                   _cancelRect.Y + (_cancelRect.Height - cFt.Height) / 2));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!EPDIsOpen) return;
        var pos = e.GetPosition(this);
        if (_tabGeoRect.Width > 0 && _tabGeoRect.Contains(pos))       { EPDMode = EditPositionMode.Geographic; InvalidateVisual(); }
        else if (_tabUtmRect.Width > 0 && _tabUtmRect.Contains(pos))  { EPDMode = EditPositionMode.UTM;        InvalidateVisual(); }
        else if (_tabMgrsRect.Width > 0 && _tabMgrsRect.Contains(pos)){ EPDMode = EditPositionMode.MGRS;       InvalidateVisual(); }
        else if (_okRect.Width > 0 && _okRect.Contains(pos))
        {
            PositionAccepted?.Invoke(this, (EditLat, EditLon, EditAlt));
            EPDIsOpen = false;
        }
        else if (_cancelRect.Width > 0 && _cancelRect.Contains(pos))
        {
            EPDIsOpen = false;
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size available) =>
        EPDIsOpen ? available : new Size(0, 0);
}

