using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Globalization;

namespace VGC.Views.Controls;

/// <summary>
/// Slider with tick marks, value display, and min/max labels — equivalent to QGC FactSlider/ValueSlider.
/// </summary>
public class FactSlider : TemplatedControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<FactSlider, double>(nameof(Value));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<FactSlider, double>(nameof(Minimum), 0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<FactSlider, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<FactSlider, double>(nameof(Step), 1);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FactSlider, string>(nameof(Label), "");

    public static readonly StyledProperty<string> UnitsProperty =
        AvaloniaProperty.Register<FactSlider, string>(nameof(Units), "");

    public static readonly StyledProperty<string> ParameterNameProperty =
        AvaloniaProperty.Register<FactSlider, string>(nameof(ParameterName), "");

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }
    public string ParameterName { get => GetValue(ParameterNameProperty); set => SetValue(ParameterNameProperty, value); }

    public event EventHandler<double>? ValueCommitted;

    public void CommitValue() => ValueCommitted?.Invoke(this, Value);
}

/// <summary>
/// Combo box for enum parameters — equivalent to QGC FactComboBox.
/// </summary>
public class FactComboBox : TemplatedControl
{
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<FactComboBox, int>(nameof(SelectedIndex), -1);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FactComboBox, string>(nameof(Label), "");

    public static readonly StyledProperty<string> ParameterNameProperty =
        AvaloniaProperty.Register<FactComboBox, string>(nameof(ParameterName), "");

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string ParameterName { get => GetValue(ParameterNameProperty); set => SetValue(ParameterNameProperty, value); }

    public ObservableCollection<FactComboBoxItem> Items { get; } = [];

    public FactComboBoxItem? SelectedItem => SelectedIndex >= 0 && SelectedIndex < Items.Count
        ? Items[SelectedIndex]
        : null;

    public event EventHandler<FactComboBoxItem?>? SelectionCommitted;

    public void CommitSelection() => SelectionCommitted?.Invoke(this, SelectedItem);
}

public sealed record FactComboBoxItem(int Value, string Label);

/// <summary>
/// Text input with validation for numeric parameters — equivalent to QGC FactTextField.
/// </summary>
public class FactTextField : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<FactTextField, string>(nameof(Text), "");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FactTextField, string>(nameof(Label), "");

    public static readonly StyledProperty<string> UnitsProperty =
        AvaloniaProperty.Register<FactTextField, string>(nameof(Units), "");

    public static readonly StyledProperty<string> ParameterNameProperty =
        AvaloniaProperty.Register<FactTextField, string>(nameof(ParameterName), "");

    public static readonly StyledProperty<string> ValidationErrorProperty =
        AvaloniaProperty.Register<FactTextField, string>(nameof(ValidationError), "");

    public static readonly StyledProperty<double?> MinValueProperty =
        AvaloniaProperty.Register<FactTextField, double?>(nameof(MinValue));

    public static readonly StyledProperty<double?> MaxValueProperty =
        AvaloniaProperty.Register<FactTextField, double?>(nameof(MaxValue));

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }
    public string ParameterName { get => GetValue(ParameterNameProperty); set => SetValue(ParameterNameProperty, value); }
    public string ValidationError { get => GetValue(ValidationErrorProperty); set => SetValue(ValidationErrorProperty, value); }
    public double? MinValue { get => GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
    public double? MaxValue { get => GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    public bool HasError => !string.IsNullOrEmpty(ValidationError);

    public event EventHandler<string>? TextCommitted;

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            ValidationError = "Value required";
            return false;
        }

        if (!double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
        {
            ValidationError = "Invalid number";
            return false;
        }

        if (MinValue.HasValue && num < MinValue.Value)
        {
            ValidationError = $"Min: {MinValue.Value}";
            return false;
        }

        if (MaxValue.HasValue && num > MaxValue.Value)
        {
            ValidationError = $"Max: {MaxValue.Value}";
            return false;
        }

        ValidationError = "";
        return true;
    }

    public void CommitText()
    {
        if (Validate())
            TextCommitted?.Invoke(this, Text);
    }
}

/// <summary>
/// Slide-to-confirm control for dangerous operations — equivalent to QGC SliderConfirmation / QGCDelayButton.
/// </summary>
public class SliderConfirmation : Control
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SliderConfirmation, string>(nameof(Text), "Slide to confirm");

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<SliderConfirmation, double>(nameof(Progress), 0);

    public static readonly StyledProperty<bool> IsConfirmedProperty =
        AvaloniaProperty.Register<SliderConfirmation, bool>(nameof(IsConfirmed), false);

    static SliderConfirmation()
    {
        AffectsRender<SliderConfirmation>(TextProperty, ProgressProperty, IsConfirmedProperty);
    }

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool IsConfirmed { get => GetValue(IsConfirmedProperty); set => SetValue(IsConfirmedProperty, value); }

    public event EventHandler? Confirmed;

    private bool _dragging;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 10 || bounds.Height < 10) return;

        var w = bounds.Width;
        var h = bounds.Height;
        var radius = h / 2;

        // Track background
        var trackBrush = new SolidColorBrush(IsConfirmed ? Color.Parse("#27ae60") : Color.Parse("#1a2c3a"));
        var trackPen = new Pen(new SolidColorBrush(IsConfirmed ? Color.Parse("#2ecc71") : Color.Parse("#3b5060")), 1.5);
        context.DrawRectangle(trackBrush, trackPen, new Rect(0, 0, w, h), radius, radius);

        // Fill progress
        if (Progress > 0 && !IsConfirmed)
        {
            var fillWidth = Math.Max(h, w * Progress);
            var fillBrush = new SolidColorBrush(Color.Parse("#2980b9"));
            context.DrawRectangle(fillBrush, null, new Rect(0, 0, fillWidth, h), radius, radius);
        }

        // Thumb handle
        if (!IsConfirmed)
        {
            var thumbX = Math.Max(2, (w - h) * Progress);
            var thumbBrush = new SolidColorBrush(Color.Parse("#3498db"));
            var thumbPen = new Pen(Brushes.White, 2);
            context.DrawEllipse(thumbBrush, thumbPen, new Point(thumbX + radius, h / 2), radius - 4, radius - 4);

            // Arrow on thumb
            var arrowPen = new Pen(Brushes.White, 2.5);
            var cx = thumbX + radius;
            var cy = h / 2;
            context.DrawLine(arrowPen, new Point(cx - 6, cy), new Point(cx + 6, cy));
            context.DrawLine(arrowPen, new Point(cx + 2, cy - 5), new Point(cx + 8, cy));
            context.DrawLine(arrowPen, new Point(cx + 2, cy + 5), new Point(cx + 8, cy));
        }

        // Text
        var textBrush = new SolidColorBrush(IsConfirmed ? Colors.White : Color.Parse("#c9d5df"));
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var displayText = IsConfirmed ? "Confirmed" : Text;
        var formattedText = new FormattedText(
            displayText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface, 13, textBrush);
        var textX = IsConfirmed ? (w - formattedText.Width) / 2 : h + 8;
        context.DrawText(formattedText, new Point(textX, (h - formattedText.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsConfirmed) return;

        var pos = e.GetPosition(this);
        var thumbX = (Bounds.Width - Bounds.Height) * Progress;
        var thumbCenter = thumbX + Bounds.Height / 2;

        if (Math.Abs(pos.X - thumbCenter) < Bounds.Height)
        {
            _dragging = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;

        var pos = e.GetPosition(this);
        var trackWidth = Bounds.Width - Bounds.Height;
        if (trackWidth <= 0) return;

        Progress = Math.Clamp((pos.X - Bounds.Height / 2) / trackWidth, 0, 1);
        InvalidateVisual();

        if (Progress >= 0.95)
        {
            Progress = 1;
            IsConfirmed = true;
            _dragging = false;
            e.Pointer.Capture(null);
            Confirmed?.Invoke(this, EventArgs.Empty);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging && !IsConfirmed)
        {
            Progress = 0;
            InvalidateVisual();
        }
        _dragging = false;
        e.Pointer.Capture(null);
    }

    public void Reset()
    {
        Progress = 0;
        IsConfirmed = false;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        return new Size(w, 44);
    }
}

/// <summary>
/// Vehicle rotation calibration indicator — equivalent to QGC VehicleRotationCal.
/// Shows 6-face orientation status (up/down/left/right/nose/tail) with color-coded states.
/// </summary>
public class VehicleRotationCal : Control
{
    public static readonly StyledProperty<CalibrationFaceStatus> StatusProperty =
        AvaloniaProperty.Register<VehicleRotationCal, CalibrationFaceStatus>(nameof(Status));

    public static readonly StyledProperty<string> FaceLabelProperty =
        AvaloniaProperty.Register<VehicleRotationCal, string>(nameof(FaceLabel), "");

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<VehicleRotationCal, string>(nameof(StatusText), "");

    static VehicleRotationCal()
    {
        AffectsRender<VehicleRotationCal>(StatusProperty, FaceLabelProperty, StatusTextProperty);
    }

    public CalibrationFaceStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public string FaceLabel { get => GetValue(FaceLabelProperty); set => SetValue(FaceLabelProperty, value); }
    public string StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 10 || bounds.Height < 10) return;

        var w = bounds.Width;
        var h = bounds.Height;
        var inset = 3.0;

        // Border color by status
        var borderColor = Status switch
        {
            CalibrationFaceStatus.Completed => Color.Parse("#27ae60"),
            CalibrationFaceStatus.InProgress => Color.Parse("#f39c12"),
            CalibrationFaceStatus.Incomplete => Color.Parse("#e74c3c"),
            _ => Color.Parse("#4e6070")
        };

        var bgBrush = new SolidColorBrush(Color.Parse("#151f28"));
        var borderPen = new Pen(new SolidColorBrush(borderColor), 2);
        context.DrawRectangle(bgBrush, borderPen, new Rect(inset, inset, w - inset * 2, h - inset * 2), 4, 4);

        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);

        // Face label (top)
        var labelText = new FormattedText(FaceLabel, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 13, Brushes.White);
        context.DrawText(labelText, new Point((w - labelText.Width) / 2, inset + 6));

        // Vehicle orientation icon (simple rectangle representing the face)
        var iconSize = Math.Min(w, h) * 0.35;
        var iconX = (w - iconSize) / 2;
        var iconY = (h - iconSize) / 2 - 4;
        var iconBrush = new SolidColorBrush(Color.Parse("#2a3a48"));
        var iconPen = new Pen(new SolidColorBrush(borderColor), 1.5);
        context.DrawRectangle(iconBrush, iconPen, new Rect(iconX, iconY, iconSize, iconSize), 3, 3);

        // Arrow or indicator inside icon
        var arrowBrush = new SolidColorBrush(borderColor);
        var arrowSize = iconSize * 0.3;
        context.DrawEllipse(arrowBrush, null,
            new Point(iconX + iconSize / 2, iconY + iconSize / 2), arrowSize / 2, arrowSize / 2);

        // Status text (bottom)
        if (!string.IsNullOrEmpty(StatusText))
        {
            var statusColor = Status switch
            {
                CalibrationFaceStatus.Completed => Color.Parse("#27ae60"),
                CalibrationFaceStatus.InProgress => Color.Parse("#f39c12"),
                _ => Color.Parse("#91a4b5")
            };
            var statusFmt = new FormattedText(StatusText, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, new SolidColorBrush(statusColor));
            context.DrawText(statusFmt, new Point((w - statusFmt.Width) / 2, h - inset - statusFmt.Height - 4));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var s = double.IsInfinity(availableSize.Width) ? 120 : Math.Min(availableSize.Width, 160);
        var h = double.IsInfinity(availableSize.Height) ? 120 : Math.Min(availableSize.Height, 160);
        return new Size(s, h);
    }
}

public enum CalibrationFaceStatus
{
    Idle,
    Incomplete,
    InProgress,
    Completed
}

/// <summary>
/// Indicator button with red/green status dot — equivalent to QGC IndicatorButton.
/// </summary>
public class IndicatorButton : Button
{
    public static readonly StyledProperty<bool> IndicatorGreenProperty =
        AvaloniaProperty.Register<IndicatorButton, bool>(nameof(IndicatorGreen), false);

    public bool IndicatorGreen
    {
        get => GetValue(IndicatorGreenProperty);
        set => SetValue(IndicatorGreenProperty, value);
    }
}

/// <summary>
/// PiP (Picture-in-Picture) view state manager.
/// </summary>
public enum PipViewState
{
    MapFull,
    VideoFull,
    MapPipVideoFull,
    VideoPipMapFull
}

public sealed class PipViewModel
{
    private PipViewState _state = PipViewState.MapFull;

    public PipViewState State => _state;

    public bool HasVideo { get; set; }

    public bool ShowPip => HasVideo && _state is PipViewState.MapPipVideoFull or PipViewState.VideoPipMapFull;

    public bool IsMapMain => _state is PipViewState.MapFull or PipViewState.VideoPipMapFull;

    public string StatusText => _state switch
    {
        PipViewState.MapFull => "Map",
        PipViewState.VideoFull => "Video",
        PipViewState.MapPipVideoFull => "Map (PiP: Video)",
        PipViewState.VideoPipMapFull => "Video (PiP: Map)",
        _ => "Unknown"
    };

    public void SwapPip()
    {
        _state = _state switch
        {
            PipViewState.MapFull when HasVideo => PipViewState.MapPipVideoFull,
            PipViewState.MapPipVideoFull => PipViewState.VideoPipMapFull,
            PipViewState.VideoPipMapFull => PipViewState.MapPipVideoFull,
            PipViewState.VideoFull => PipViewState.VideoPipMapFull,
            _ => _state
        };
    }

    public void ToggleFullScreen()
    {
        _state = _state switch
        {
            PipViewState.MapPipVideoFull => PipViewState.VideoFull,
            PipViewState.VideoPipMapFull => PipViewState.MapFull,
            PipViewState.MapFull when HasVideo => PipViewState.MapPipVideoFull,
            PipViewState.VideoFull => PipViewState.VideoPipMapFull,
            _ => _state
        };
    }
}

// ════════════════════════════════════════════════════════════════
// HORIZONTAL FACT VALUE GRID
// QGC equivalent: QmlControls/HorizontalFactValueGrid.qml
// Multi-column telemetry label + value grid shown below the HUD.
// ════════════════════════════════════════════════════════════════

/// <summary>A single telemetry cell: label + value + units.</summary>
public sealed record FactValueCell(string Label, string Value, string Units = "");

/// <summary>
/// Horizontal grid of label/value pairs — used in telemetry bars at bottom of FlyView.
/// Equivalent to QGC QmlControls/HorizontalFactValueGrid.qml
/// Renders N columns × M rows of left-aligned label + right-aligned value pairs.
/// </summary>
public sealed class HorizontalFactValueGrid : Control
{
    public static readonly StyledProperty<IReadOnlyList<FactValueCell>?> CellsProperty =
        AvaloniaProperty.Register<HorizontalFactValueGrid, IReadOnlyList<FactValueCell>?>(nameof(Cells));

    public static readonly StyledProperty<int> ColumnCountProperty =
        AvaloniaProperty.Register<HorizontalFactValueGrid, int>(nameof(ColumnCount), 3);

    static HorizontalFactValueGrid()
    {
        AffectsRender<HorizontalFactValueGrid>(CellsProperty, ColumnCountProperty);
    }

    public IReadOnlyList<FactValueCell>? Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    public int ColumnCount
    {
        get => GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var cells = Cells;
        if (cells is null || cells.Count == 0) return;
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 20 || h < 12) return;

        var cols   = Math.Max(1, ColumnCount);
        var rows   = (int)Math.Ceiling(cells.Count / (double)cols);
        var colW   = w / cols;
        var rowH   = h / rows;
        var tf     = new Typeface("Segoe UI");
        var tfBold = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var labelBrush = new SolidColorBrush(QgcColors.Text);
        var valueBrush = new SolidColorBrush(Colors.White);

        for (var i = 0; i < cells.Count; i++)
        {
            var col  = i % cols;
            var row  = i / cols;
            var cell = cells[i];
            var cx   = col * colW;
            var cy   = row * rowH;

            // Label (small, top)
            var labelFs = ScreenMetrics.SmallFontPointSize;
            var lFmt = new FormattedText(cell.Label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, labelFs, labelBrush);
            context.DrawText(lFmt, new Point(cx + 4, cy + 2));

            // Value (larger, below label)
            var valText = string.IsNullOrEmpty(cell.Units) ? cell.Value : $"{cell.Value} {cell.Units}";
            var vFmt = new FormattedText(valText, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tfBold, ScreenMetrics.DefaultFontPixelHeight, valueBrush);
            context.DrawText(vFmt, new Point(cx + 4, cy + lFmt.Height + 3));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var cols = Math.Max(1, ColumnCount);
        var rows = (int)Math.Ceiling((Cells?.Count ?? 0) / (double)cols);
        var rowH = ScreenMetrics.DefaultFontPixelHeight + ScreenMetrics.SmallFontPointSize + 8;
        var w = double.IsInfinity(availableSize.Width)
            ? cols * ScreenMetrics.DefaultFontPixelWidth * 12
            : availableSize.Width;
        return new Size(w, rows * rowH);
    }
}

// ────────────────────────────────────────────────────────────────
// 8. FlyViewInstrumentPanel
//    QGC equivalent: FlyView/FlyViewInstrumentPanel.qml
//    Thin wrapper around a user-selectable instrument control.
//    Exposes ExtraInset so the right-side inset panel can account
//    for the instrument's extra vertical space.
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Container for the user-selectable fly-view instrument panel.
/// Mirrors QGC's <c>FlyViewInstrumentPanel.qml</c> (a <c>SelectableControl</c> wrapper).
/// The actual instrument widget is set as <see cref="ContentControl.Content"/>.
/// </summary>
public class FlyViewInstrumentPanel : ContentControl
{
    public static readonly StyledProperty<double> ExtraInsetProperty =
        AvaloniaProperty.Register<FlyViewInstrumentPanel, double>(nameof(ExtraInset), 0);

    public static readonly StyledProperty<string> InstrumentNameProperty =
        AvaloniaProperty.Register<FlyViewInstrumentPanel, string>(nameof(InstrumentName), "Default");

    /// <summary>
    /// Extra inset pixels consumed by this instrument below the standard right-panel edge.
    /// Bind to the right-panel inset calculation so layout compensates for the instrument height.
    /// </summary>
    public double ExtraInset
    {
        get => GetValue(ExtraInsetProperty);
        set => SetValue(ExtraInsetProperty, value);
    }

    /// <summary>
    /// Human-readable name of the currently selected instrument (e.g. "Instrument Values").
    /// Matches the QGC <c>instrumentQmlFile2</c> fact value.
    /// </summary>
    public string InstrumentName
    {
        get => GetValue(InstrumentNameProperty);
        set => SetValue(InstrumentNameProperty, value);
    }
}

// ═══════════════════════════════════════════════════════════════
// Labelled Row Controls
// Equivalent to QGC QmlControls/Labelled*.qml — label + control rows.
// All are TemplatedControls; visual composition comes from AXAML templates.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Horizontal row: expanding label on the left, button on the right.
/// Equivalent to QGC QmlControls/LabelledButton.qml
/// </summary>
public sealed class LabelledButton : TemplatedControl
{
    public static readonly StyledProperty<string> LabelTextProperty =
        AvaloniaProperty.Register<LabelledButton, string>(nameof(LabelText), "");
    public static readonly StyledProperty<string> ButtonTextProperty =
        AvaloniaProperty.Register<LabelledButton, string>(nameof(ButtonText), "");
    public static readonly StyledProperty<double> ButtonPreferredWidthProperty =
        AvaloniaProperty.Register<LabelledButton, double>(nameof(ButtonPreferredWidth), -1);

    public string LabelText          { get => GetValue(LabelTextProperty);          set => SetValue(LabelTextProperty, value); }
    public string ButtonText         { get => GetValue(ButtonTextProperty);         set => SetValue(ButtonTextProperty, value); }
    public double ButtonPreferredWidth { get => GetValue(ButtonPreferredWidthProperty); set => SetValue(ButtonPreferredWidthProperty, value); }

    public event EventHandler? Clicked;
    public void RaiseClicked() => Clicked?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Horizontal row: expanding label on the left, combo box on the right.
/// Equivalent to QGC QmlControls/LabelledComboBox.qml
/// </summary>
public sealed class LabelledComboBox : TemplatedControl
{
    public static readonly StyledProperty<string> LabelTextProperty =
        AvaloniaProperty.Register<LabelledComboBox, string>(nameof(LabelText), "");
    public static readonly StyledProperty<IReadOnlyList<string>?> ItemsProperty =
        AvaloniaProperty.Register<LabelledComboBox, IReadOnlyList<string>?>(nameof(Items));
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<LabelledComboBox, int>(nameof(SelectedIndex));
    public static readonly StyledProperty<double> ComboBoxPreferredWidthProperty =
        AvaloniaProperty.Register<LabelledComboBox, double>(nameof(ComboBoxPreferredWidth), -1);

    public string LabelText          { get => GetValue(LabelTextProperty);          set => SetValue(LabelTextProperty, value); }
    public IReadOnlyList<string>? Items { get => GetValue(ItemsProperty);           set => SetValue(ItemsProperty, value); }
    public int    SelectedIndex      { get => GetValue(SelectedIndexProperty);      set => SetValue(SelectedIndexProperty, value); }
    public double ComboBoxPreferredWidth { get => GetValue(ComboBoxPreferredWidthProperty); set => SetValue(ComboBoxPreferredWidthProperty, value); }

    public event EventHandler<int>? SelectionChanged;
    public void RaiseSelectionChanged(int index) => SelectionChanged?.Invoke(this, index);
}

/// <summary>
/// Horizontal row: label on the left, read-only value label on the right.
/// Equivalent to QGC QmlControls/LabelledLabel.qml
/// </summary>
public sealed class LabelledLabel : TemplatedControl
{
    public static readonly StyledProperty<string> LabelTextProperty =
        AvaloniaProperty.Register<LabelledLabel, string>(nameof(LabelText), "");
    public static readonly StyledProperty<string> ValueTextProperty =
        AvaloniaProperty.Register<LabelledLabel, string>(nameof(ValueText), "");
    public static readonly StyledProperty<double> ValuePreferredWidthProperty =
        AvaloniaProperty.Register<LabelledLabel, double>(nameof(ValuePreferredWidth), -1);

    public string LabelText  { get => GetValue(LabelTextProperty);  set => SetValue(LabelTextProperty, value); }
    public string ValueText  { get => GetValue(ValueTextProperty);  set => SetValue(ValueTextProperty, value); }
    public double ValuePreferredWidth { get => GetValue(ValuePreferredWidthProperty); set => SetValue(ValuePreferredWidthProperty, value); }
}

/// <summary>
/// Horizontal row: expanding label on the left, slider on the right.
/// Equivalent to QGC QmlControls/LabelledSlider.qml
/// </summary>
public sealed class LabelledSlider : TemplatedControl
{
    public static readonly StyledProperty<string> LabelTextProperty =
        AvaloniaProperty.Register<LabelledSlider, string>(nameof(LabelText), "");
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<LabelledSlider, double>(nameof(Minimum), 0.0);
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<LabelledSlider, double>(nameof(Maximum), 100.0);
    public static readonly StyledProperty<double> SliderValueProperty =
        AvaloniaProperty.Register<LabelledSlider, double>(nameof(SliderValue));
    public static readonly StyledProperty<double> SliderPreferredWidthProperty =
        AvaloniaProperty.Register<LabelledSlider, double>(nameof(SliderPreferredWidth), -1);

    public string LabelText  { get => GetValue(LabelTextProperty);  set => SetValue(LabelTextProperty, value); }
    public double Minimum    { get => GetValue(MinimumProperty);    set => SetValue(MinimumProperty, value); }
    public double Maximum    { get => GetValue(MaximumProperty);    set => SetValue(MaximumProperty, value); }
    public double SliderValue { get => GetValue(SliderValueProperty); set => SetValue(SliderValueProperty, value); }
    public double SliderPreferredWidth { get => GetValue(SliderPreferredWidthProperty); set => SetValue(SliderPreferredWidthProperty, value); }

    public event EventHandler<double>? SliderValueChanged;
    public void RaiseValueChanged(double v) => SliderValueChanged?.Invoke(this, v);
}

/// <summary>
/// Horizontal row showing a vehicle property: label on the left, value text right-aligned.
/// Max-width of value is capped to prevent overflow.
/// Equivalent to QGC QmlControls/VehicleSummaryRow.qml
/// </summary>
public sealed class VehicleSummaryRow : Control
{
    public static readonly StyledProperty<string> RowLabelProperty =
        AvaloniaProperty.Register<VehicleSummaryRow, string>(nameof(RowLabel), "");
    public static readonly StyledProperty<string> RowValueProperty =
        AvaloniaProperty.Register<VehicleSummaryRow, string>(nameof(RowValue), "");
    public static readonly StyledProperty<Color> RowValueColorProperty =
        AvaloniaProperty.Register<VehicleSummaryRow, Color>(nameof(RowValueColor), QgcColors.Text);

    static VehicleSummaryRow()
    {
        AffectsRender<VehicleSummaryRow>(RowLabelProperty, RowValueProperty, RowValueColorProperty);
    }

    public string RowLabel      { get => GetValue(RowLabelProperty);      set => SetValue(RowLabelProperty, value); }
    public string RowValue      { get => GetValue(RowValueProperty);      set => SetValue(RowValueProperty, value); }
    public Color  RowValueColor { get => GetValue(RowValueColorProperty); set => SetValue(RowValueColorProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 10) return;

        var tf     = new Typeface("Segoe UI");
        var sz     = ScreenMetrics.DefaultFontPointSize;
        var valMaxW = ScreenMetrics.DefaultFontPixelWidth * 20;

        var lblFt = new FormattedText(RowLabel, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, tf, sz,
            new SolidColorBrush(QgcColors.Text));

        var valFt = new FormattedText(RowValue, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, tf, sz,
            new SolidColorBrush(RowValueColor));

        double valDisplayW = Math.Min(valFt.Width, valMaxW);
        double labelW      = Math.Max(0, w - valDisplayW - ScreenMetrics.DefaultFontPixelWidth);

        double cy = (h - lblFt.Height) / 2;
        ctx.DrawText(lblFt, new Point(0, cy));

        // Value text right-aligned, clipped to max width
        using (ctx.PushClip(new Rect(w - valDisplayW, 0, valDisplayW, h)))
        {
            ctx.DrawText(valFt, new Point(w - valFt.Width, cy));
        }
        _ = labelW; // used conceptually for spacing
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight + 4);
    }
}

// ═══════════════════════════════════════════════════════════════
// QGCMarqueeLabel
// Equivalent to QGC QmlControls/QGCMarqueeLabel.qml
// Scrolls text horizontally when it overflows MaxDisplayWidth.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Text label that scrolls horizontally (marquee) when its content is wider than
/// <see cref="MaxDisplayWidth"/>. When the text fits, it renders statically.
/// Equivalent to QGC QmlControls/QGCMarqueeLabel.qml
/// </summary>
public sealed class QGCMarqueeLabel : Control
{
    private const double Gap          = 32; // px gap between end of text and its repeat
    private const double ScrollPxSec  = 40; // default scroll speed

    public static readonly StyledProperty<string> MarqueeTextProperty =
        AvaloniaProperty.Register<QGCMarqueeLabel, string>(nameof(MarqueeText), "");
    public static readonly StyledProperty<Color> MarqueeColorProperty =
        AvaloniaProperty.Register<QGCMarqueeLabel, Color>(nameof(MarqueeColor), QgcColors.Text);
    public static readonly StyledProperty<double> MaxDisplayWidthProperty =
        AvaloniaProperty.Register<QGCMarqueeLabel, double>(nameof(MaxDisplayWidth), double.PositiveInfinity);
    public static readonly StyledProperty<double> ScrollSpeedProperty =
        AvaloniaProperty.Register<QGCMarqueeLabel, double>(nameof(ScrollSpeed), ScrollPxSec);

    static QGCMarqueeLabel()
    {
        AffectsRender<QGCMarqueeLabel>(MarqueeTextProperty, MarqueeColorProperty);
        AffectsMeasure<QGCMarqueeLabel>(MarqueeTextProperty, MaxDisplayWidthProperty);
    }

    public string MarqueeText   { get => GetValue(MarqueeTextProperty);   set => SetValue(MarqueeTextProperty, value); }
    public Color  MarqueeColor  { get => GetValue(MarqueeColorProperty);  set => SetValue(MarqueeColorProperty, value); }
    public double MaxDisplayWidth { get => GetValue(MaxDisplayWidthProperty); set => SetValue(MaxDisplayWidthProperty, value); }
    public double ScrollSpeed   { get => GetValue(ScrollSpeedProperty);   set => SetValue(ScrollSpeedProperty, value); }

    private DispatcherTimer? _timer;
    private double _scrollX;
    private double _lastTextWidth;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 1 || string.IsNullOrEmpty(MarqueeText)) return;

        var ft = MakeFt();
        _lastTextWidth = ft.Width;
        bool scrolling = _lastTextWidth > w;

        UpdateTimer(scrolling);

        using (ctx.PushClip(new Rect(0, 0, w, h)))
        {
            double y = (h - ft.Height) / 2;
            ctx.DrawText(ft, new Point(scrolling ? _scrollX : 0, y));
            if (scrolling)
                ctx.DrawText(ft, new Point(_scrollX + _lastTextWidth + Gap, y));
        }
    }

    private FormattedText MakeFt() =>
        new(MarqueeText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), ScreenMetrics.DefaultFontPointSize,
            new SolidColorBrush(MarqueeColor));

    private void UpdateTimer(bool? needsScrolling = null)
    {
        var need = needsScrolling ?? (_lastTextWidth > Bounds.Width && Bounds.Width > 1);
        if (need && (_timer == null || !_timer.IsEnabled))
        {
            _scrollX = 0;
            _timer   = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += (_, _) =>
            {
                var step = ScrollSpeed / 30.0;
                _scrollX -= step;
                if (_scrollX <= -(_lastTextWidth + Gap))
                    _scrollX = 0;
                InvalidateVisual();
            };
            _timer.Start();
        }
        else if (!need)
        {
            _timer?.Stop();
            _timer   = null;
            _scrollX = 0;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ft = MakeFt();
        _lastTextWidth = ft.Width;
        var maxW = double.IsInfinity(MaxDisplayWidth) ? availableSize.Width : MaxDisplayWidth;
        if (double.IsInfinity(maxW)) maxW = _lastTextWidth;
        var w = Math.Min(_lastTextWidth, maxW);
        return new Size(w, ft.Height);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FactLabel  (#144 — QGC FactLabel: read-only Fact value display)
// Renders the bound value text with an optional smaller units suffix.
// ValueText and UnitsText are set externally from a Fact binding.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class FactLabel : Control
{
    public static readonly StyledProperty<string> ValueTextProperty =
        AvaloniaProperty.Register<FactLabel, string>(nameof(ValueText), "—");
    public static readonly StyledProperty<string> UnitsTextProperty =
        AvaloniaProperty.Register<FactLabel, string>(nameof(UnitsText), string.Empty);
    public static readonly StyledProperty<Color>  LabelColorProperty =
        AvaloniaProperty.Register<FactLabel, Color>(nameof(LabelColor), QgcColors.Text);
    public static readonly StyledProperty<double> FactFontSizeProperty =
        AvaloniaProperty.Register<FactLabel, double>(nameof(FontSize), ScreenMetrics.DefaultFontPixelHeight);

    static FactLabel()
    {
        AffectsRender<FactLabel>(ValueTextProperty, UnitsTextProperty, LabelColorProperty, FactFontSizeProperty);
    }

    public string ValueText { get => GetValue(ValueTextProperty);    set => SetValue(ValueTextProperty, value); }
    public string UnitsText { get => GetValue(UnitsTextProperty);    set => SetValue(UnitsTextProperty, value); }
    public Color  LabelColor { get => GetValue(LabelColorProperty);  set => SetValue(LabelColorProperty, value); }
    public double FontSize   { get => GetValue(FactFontSizeProperty); set => SetValue(FactFontSizeProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var brush     = new SolidColorBrush(LabelColor);
        var unitBrush = new SolidColorBrush(QgcColors.TextSecondary);
        var fs        = FontSize;
        var ftVal     = new FormattedText(ValueText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, fs, brush);
        ctx.DrawText(ftVal, new Point(0, 0));
        if (!string.IsNullOrEmpty(UnitsText))
        {
            var ftUnit = new FormattedText(UnitsText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, fs * 0.75, unitBrush);
            ctx.DrawText(ftUnit, new Point(ftVal.Width + 2, fs * 0.2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var fs    = FontSize;
        var brush = new SolidColorBrush(LabelColor);
        var ftVal = new FormattedText(ValueText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, fs, brush);
        double w = ftVal.Width;
        if (!string.IsNullOrEmpty(UnitsText))
        {
            var ftUnit = new FormattedText(UnitsText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, fs * 0.75, new SolidColorBrush(QgcColors.TextSecondary));
            w += 2 + ftUnit.Width;
        }
        return new Size(w, fs * 1.2);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FactUnitLabel  (#145 — displays value + unit in a tight horizontal pair)
// Renders a large value on the left and a small unit label to its right,
// vertically centred.  Used in instrument panels and telemetry value cells.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class FactUnitLabel : Control
{
    public static readonly StyledProperty<string> FulValueTextProperty =
        AvaloniaProperty.Register<FactUnitLabel, string>(nameof(ValueText), "—");
    public static readonly StyledProperty<string> FulUnitTextProperty =
        AvaloniaProperty.Register<FactUnitLabel, string>(nameof(UnitText), string.Empty);
    public static readonly StyledProperty<Color>  FulValueColorProperty =
        AvaloniaProperty.Register<FactUnitLabel, Color>(nameof(ValueColor), QgcColors.Text);
    public static readonly StyledProperty<double> FulValueFontSizeProperty =
        AvaloniaProperty.Register<FactUnitLabel, double>(nameof(ValueFontSize), ScreenMetrics.DefaultFontPixelHeight);

    static FactUnitLabel()
    {
        AffectsRender<FactUnitLabel>(FulValueTextProperty, FulUnitTextProperty, FulValueColorProperty, FulValueFontSizeProperty);
    }

    public string ValueText     { get => GetValue(FulValueTextProperty);     set => SetValue(FulValueTextProperty, value); }
    public string UnitText      { get => GetValue(FulUnitTextProperty);      set => SetValue(FulUnitTextProperty, value); }
    public Color  ValueColor    { get => GetValue(FulValueColorProperty);    set => SetValue(FulValueColorProperty, value); }
    public double ValueFontSize { get => GetValue(FulValueFontSizeProperty); set => SetValue(FulValueFontSizeProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var h          = Bounds.Height;
        var vfs        = ValueFontSize;
        var ufs        = vfs * 0.65;
        var valueBrush = new SolidColorBrush(ValueColor);
        var unitBrush  = new SolidColorBrush(QgcColors.TextSecondary);
        var ftVal      = new FormattedText(ValueText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, vfs, valueBrush);
        ctx.DrawText(ftVal, new Point(0, (h - ftVal.Height) / 2));
        if (!string.IsNullOrEmpty(UnitText))
        {
            var ftUnit = new FormattedText(UnitText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, ufs, unitBrush);
            ctx.DrawText(ftUnit, new Point(ftVal.Width + 2, (h - ftUnit.Height) / 2 + ftVal.Height * 0.15));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var vfs   = ValueFontSize;
        var ftVal = new FormattedText(ValueText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, vfs, new SolidColorBrush(ValueColor));
        double w = ftVal.Width;
        double h = ftVal.Height;
        if (!string.IsNullOrEmpty(UnitText))
        {
            var ftUnit = new FormattedText(UnitText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, vfs * 0.65, new SolidColorBrush(QgcColors.TextSecondary));
            w += 2 + ftUnit.Width;
        }
        return new Size(w, h);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// QGCColoredImage  (#146 — QGC ColoredImage: SVG-like tinted geometry icon)
// Scales a Geometry to fill the control bounds (preserving aspect ratio) and
// fills it with IconColor.  Equivalent to QGC's ColoredImage / MavlinkIcon.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class QGCColoredImage : Control
{
    public static readonly StyledProperty<Geometry?> IconPathProperty =
        AvaloniaProperty.Register<QGCColoredImage, Geometry?>(nameof(IconPath));
    public static readonly StyledProperty<Color> IconColorProperty =
        AvaloniaProperty.Register<QGCColoredImage, Color>(nameof(IconColor), QgcColors.Text);

    static QGCColoredImage()
    {
        AffectsRender<QGCColoredImage>(IconPathProperty, IconColorProperty);
    }

    public Geometry? IconPath  { get => GetValue(IconPathProperty);  set => SetValue(IconPathProperty, value); }
    public Color     IconColor { get => GetValue(IconColorProperty); set => SetValue(IconColorProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        if (IconPath is not { } geo) return;
        var gb = geo.Bounds;
        if (gb.Width <= 0 || gb.Height <= 0) return;
        var bw = Bounds.Width;
        var bh = Bounds.Height;
        double scale = Math.Min(bw / gb.Width, bh / gb.Height);
        double tx = (bw - gb.Width  * scale) / 2.0 - gb.X * scale;
        double ty = (bh - gb.Height * scale) / 2.0 - gb.Y * scale;
        using var _ = ctx.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(tx, ty));
        ctx.DrawGeometry(new SolidColorBrush(IconColor), null, geo);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = double.IsInfinity(availableSize.Width)  ? dfw * 3 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? dfh * 2 : availableSize.Height;
        return new Size(w, h);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TelemetryValueCell  (#154 — QGC TelemetryValues cell: label/value/unit stack)
// Vertical three-row cell used in FlyView instrument panels.
// Small label on top, large colored value in middle, small unit below.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class TelemetryValueCell : Control
{
    public static readonly StyledProperty<string> CellLabelProperty =
        AvaloniaProperty.Register<TelemetryValueCell, string>(nameof(CellLabel), string.Empty);
    public static readonly StyledProperty<string> CellValueProperty =
        AvaloniaProperty.Register<TelemetryValueCell, string>(nameof(CellValue), "—");
    public static readonly StyledProperty<string> CellUnitProperty =
        AvaloniaProperty.Register<TelemetryValueCell, string>(nameof(CellUnit), string.Empty);
    public static readonly StyledProperty<Color>  CellValueColorProperty =
        AvaloniaProperty.Register<TelemetryValueCell, Color>(nameof(CellValueColor), QgcColors.Text);

    static TelemetryValueCell()
    {
        AffectsRender<TelemetryValueCell>(CellLabelProperty, CellValueProperty, CellUnitProperty, CellValueColorProperty);
    }

    public string CellLabel      { get => GetValue(CellLabelProperty);      set => SetValue(CellLabelProperty, value); }
    public string CellValue      { get => GetValue(CellValueProperty);      set => SetValue(CellValueProperty, value); }
    public string CellUnit       { get => GetValue(CellUnitProperty);       set => SetValue(CellUnitProperty, value); }
    public Color  CellValueColor { get => GetValue(CellValueColorProperty); set => SetValue(CellValueColorProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var dfh  = ScreenMetrics.DefaultFontPixelHeight;
        var w    = Bounds.Width;

        // Small label (top)
        if (!string.IsNullOrEmpty(CellLabel))
        {
            var ft = new FormattedText(CellLabel, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.TextSecondary));
            ctx.DrawText(ft, new Point((w - ft.Width) / 2, 0));
        }

        // Large value (middle)
        double labelH = string.IsNullOrEmpty(CellLabel) ? 0 : dfh * 0.75 + 1;
        var ftVal = new FormattedText(CellValue, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 1.1,
            new SolidColorBrush(CellValueColor));
        ctx.DrawText(ftVal, new Point((w - ftVal.Width) / 2, labelH));

        // Small unit (bottom)
        if (!string.IsNullOrEmpty(CellUnit))
        {
            double valueH = labelH + dfh * 1.1 + 1;
            var ftUnit = new FormattedText(CellUnit, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.7,
                new SolidColorBrush(QgcColors.TextSecondary));
            ctx.DrawText(ftUnit, new Point((w - ftUnit.Width) / 2, valueH));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh  = ScreenMetrics.DefaultFontPixelHeight;
        var dfw  = ScreenMetrics.DefaultFontPixelWidth;
        var ftV  = new FormattedText(CellValue, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 1.1, new SolidColorBrush(QgcColors.Text));
        double w = ftV.Width + dfw;
        double h = (string.IsNullOrEmpty(CellLabel) ? 0 : dfh * 0.75 + 1)
                 + dfh * 1.1
                 + (string.IsNullOrEmpty(CellUnit)  ? 0 : dfh * 0.7  + 1);
        return new Size(Math.Max(w, dfw * 4), h);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TagLabel  (#155 — colored rounded-rect chip with text)
// Used as status badges, mode tags, parameter type labels, etc.
// Background color, text, and corner radius are all configurable.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class TagLabel : Control
{
    public static readonly StyledProperty<string> TagTextProperty =
        AvaloniaProperty.Register<TagLabel, string>(nameof(TagText), string.Empty);
    public static readonly StyledProperty<Color>  TagColorProperty =
        AvaloniaProperty.Register<TagLabel, Color>(nameof(TagColor), QgcColors.Button);
    public static readonly StyledProperty<Color>  TagTextColorProperty =
        AvaloniaProperty.Register<TagLabel, Color>(nameof(TagTextColor), QgcColors.ButtonText);
    public static readonly StyledProperty<double> TagCornerRadiusProperty =
        AvaloniaProperty.Register<TagLabel, double>(nameof(TagCornerRadius), 10.0);

    static TagLabel()
    {
        AffectsRender<TagLabel>(TagTextProperty, TagColorProperty, TagTextColorProperty, TagCornerRadiusProperty);
    }

    public string TagText         { get => GetValue(TagTextProperty);         set => SetValue(TagTextProperty, value); }
    public Color  TagColor        { get => GetValue(TagColorProperty);        set => SetValue(TagColorProperty, value); }
    public Color  TagTextColor    { get => GetValue(TagTextColorProperty);    set => SetValue(TagTextColorProperty, value); }
    public double TagCornerRadius { get => GetValue(TagCornerRadiusProperty); set => SetValue(TagCornerRadiusProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(Bounds.Size);
        var r      = TagCornerRadius;
        ctx.DrawRectangle(new SolidColorBrush(TagColor), null, bounds, r, r);
        if (!string.IsNullOrEmpty(TagText))
        {
            var dfh = ScreenMetrics.DefaultFontPixelHeight;
            var ft  = new FormattedText(TagText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
                new SolidColorBrush(TagTextColor));
            ctx.DrawText(ft, new Point((bounds.Width - ft.Width) / 2, (bounds.Height - ft.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        if (string.IsNullOrEmpty(TagText)) return new Size(dfw * 4, dfh * 1.4);
        var ft = new FormattedText(TagText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85, new SolidColorBrush(TagTextColor));
        return new Size(ft.Width + dfw * 1.2, dfh * 1.4);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// NumericSpinBox  (#156 — numeric input with ＋/－ increment buttons)
// Renders: [ − ] [ value text ] [ + ]  in a horizontal strip.
// Step controls the increment size; value is clamped to MinValue/MaxValue.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class NumericSpinBox : Control
{
    public static readonly StyledProperty<double> SpinValueProperty =
        AvaloniaProperty.Register<NumericSpinBox, double>(nameof(SpinValue), 0.0);
    public static readonly StyledProperty<double> SpinMinValueProperty =
        AvaloniaProperty.Register<NumericSpinBox, double>(nameof(MinValue), double.NegativeInfinity);
    public static readonly StyledProperty<double> SpinMaxValueProperty =
        AvaloniaProperty.Register<NumericSpinBox, double>(nameof(MaxValue), double.PositiveInfinity);
    public static readonly StyledProperty<double> SpinStepProperty =
        AvaloniaProperty.Register<NumericSpinBox, double>(nameof(Step), 1.0);
    public static readonly StyledProperty<string> SpinFormatProperty =
        AvaloniaProperty.Register<NumericSpinBox, string>(nameof(SpinFormat), "G");

    static NumericSpinBox()
    {
        AffectsRender<NumericSpinBox>(SpinValueProperty, SpinMinValueProperty, SpinMaxValueProperty, SpinStepProperty, SpinFormatProperty);
        FocusableProperty.OverrideMetadata<NumericSpinBox>(new StyledPropertyMetadata<bool>(true));
    }

    public double SpinValue { get => GetValue(SpinValueProperty);   set => SetValue(SpinValueProperty, Math.Clamp(value, MinValue, MaxValue)); }
    public double MinValue  { get => GetValue(SpinMinValueProperty); set => SetValue(SpinMinValueProperty, value); }
    public double MaxValue  { get => GetValue(SpinMaxValueProperty); set => SetValue(SpinMaxValueProperty, value); }
    public double Step      { get => GetValue(SpinStepProperty);     set => SetValue(SpinStepProperty, value); }
    public string SpinFormat{ get => GetValue(SpinFormatProperty);   set => SetValue(SpinFormatProperty, value); }

    public event EventHandler<double>? ValueChanged;

    private const double BtnW = 28;

    public override void Render(DrawingContext ctx)
    {
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);
        var h      = bounds.Height;
        double btnH = h;
        var r      = ScreenMetrics.DefaultBorderRadius;

        // Border
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.Button), null, bounds, r, r);

        // Minus button area
        var minusRect = new Rect(0, 0, BtnW, btnH);
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.ButtonFill), null, minusRect.Deflate(1), r, r);
        var ftMinus = new FormattedText("−", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh,
            new SolidColorBrush(SpinValue <= MinValue ? QgcColors.DisabledText : QgcColors.ButtonText));
        ctx.DrawText(ftMinus, new Point((BtnW - ftMinus.Width) / 2, (h - ftMinus.Height) / 2));

        // Plus button area
        var plusRect = new Rect(bounds.Width - BtnW, 0, BtnW, btnH);
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.ButtonFill), null, plusRect.Deflate(1), r, r);
        var ftPlus = new FormattedText("+", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh,
            new SolidColorBrush(SpinValue >= MaxValue ? QgcColors.DisabledText : QgcColors.ButtonText));
        ctx.DrawText(ftPlus, new Point(bounds.Width - BtnW + (BtnW - ftPlus.Width) / 2, (h - ftPlus.Height) / 2));

        // Value text (middle)
        string display = SpinValue.ToString(SpinFormat, System.Globalization.CultureInfo.CurrentUICulture);
        var ftVal = new FormattedText(display, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9, new SolidColorBrush(QgcColors.Text));
        double midX = BtnW + (bounds.Width - BtnW * 2 - ftVal.Width) / 2;
        ctx.DrawText(ftVal, new Point(midX, (h - ftVal.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var px = e.GetPosition(this).X;
        if (px < BtnW)
        {
            var nv = Math.Max(MinValue, SpinValue - Step);
            SpinValue = nv;
            ValueChanged?.Invoke(this, nv);
        }
        else if (px > Bounds.Width - BtnW)
        {
            var nv = Math.Min(MaxValue, SpinValue + Step);
            SpinValue = nv;
            ValueChanged?.Invoke(this, nv);
        }
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfw  = ScreenMetrics.DefaultFontPixelWidth;
        double w = BtnW * 2 + dfw * 5;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// IconLabel  (#157 — geometry icon + text label, side by side)
// Renders a scaled geometry icon on the left and a text string to its right,
// both vertically centred.  Useful for labeled action items in panels.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class IconLabel : Control
{
    public static readonly StyledProperty<Geometry?> ILIconGeometryProperty =
        AvaloniaProperty.Register<IconLabel, Geometry?>(nameof(ILIconGeometry));
    public static readonly StyledProperty<string> ILLabelTextProperty =
        AvaloniaProperty.Register<IconLabel, string>(nameof(LabelText), string.Empty);
    public static readonly StyledProperty<Color>  ILIconColorProperty =
        AvaloniaProperty.Register<IconLabel, Color>(nameof(IconColor), QgcColors.Text);
    public static readonly StyledProperty<Color>  ILTextColorProperty =
        AvaloniaProperty.Register<IconLabel, Color>(nameof(TextColor), QgcColors.Text);
    public static readonly StyledProperty<double> ILIconSizeProperty =
        AvaloniaProperty.Register<IconLabel, double>(nameof(IconSize), 16.0);
    public static readonly StyledProperty<double> ILSpacingProperty =
        AvaloniaProperty.Register<IconLabel, double>(nameof(Spacing), 4.0);

    static IconLabel()
    {
        AffectsRender<IconLabel>(ILIconGeometryProperty, ILLabelTextProperty, ILIconColorProperty,
            ILTextColorProperty, ILIconSizeProperty, ILSpacingProperty);
    }

    public Geometry? ILIconGeometry { get => GetValue(ILIconGeometryProperty); set => SetValue(ILIconGeometryProperty, value); }
    public string    LabelText      { get => GetValue(ILLabelTextProperty);     set => SetValue(ILLabelTextProperty, value); }
    public Color     IconColor      { get => GetValue(ILIconColorProperty);     set => SetValue(ILIconColorProperty, value); }
    public Color     TextColor      { get => GetValue(ILTextColorProperty);     set => SetValue(ILTextColorProperty, value); }
    public double    IconSize       { get => GetValue(ILIconSizeProperty);      set => SetValue(ILIconSizeProperty, value); }
    public double    Spacing        { get => GetValue(ILSpacingProperty);       set => SetValue(ILSpacingProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var h   = Bounds.Height;
        double x = 0;

        if (ILIconGeometry is { } geo)
        {
            var gb = geo.Bounds;
            if (gb.Width > 0 && gb.Height > 0)
            {
                double sz    = IconSize;
                double scale = Math.Min(sz / gb.Width, sz / gb.Height);
                double ty    = (h - gb.Height * scale) / 2.0 - gb.Y * scale;
                using var _ = ctx.PushTransform(
                    Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x - gb.X * scale, ty));
                ctx.DrawGeometry(new SolidColorBrush(IconColor), null, geo);
            }
            x += IconSize + Spacing;
        }

        if (!string.IsNullOrEmpty(LabelText))
        {
            var ft = new FormattedText(LabelText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(TextColor));
            ctx.DrawText(ft, new Point(x, (h - ft.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = 0;
        if (ILIconGeometry != null) w += IconSize + Spacing;
        if (!string.IsNullOrEmpty(LabelText))
        {
            var ft = new FormattedText(LabelText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9, new SolidColorBrush(TextColor));
            w += ft.Width;
        }
        return new Size(Math.Max(w, 20), Math.Max(IconSize, dfh));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// MissionItemIndexBadge  (#164 — circular sequence-number badge for waypoints)
// Filled circle with the item's 1-based sequence number centered in white text.
// Used in map overlays and mission item list rows.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MissionItemIndexBadge : Control
{
    public static readonly StyledProperty<int>    SequenceIndexProperty =
        AvaloniaProperty.Register<MissionItemIndexBadge, int>(nameof(SequenceIndex), 1);
    public static readonly StyledProperty<Color>  BadgeFillColorProperty =
        AvaloniaProperty.Register<MissionItemIndexBadge, Color>(nameof(BadgeFillColor), QgcColors.ColorBlue);
    public static readonly StyledProperty<bool>   MissionItemIsSelectedProperty =
        AvaloniaProperty.Register<MissionItemIndexBadge, bool>(nameof(IsSelected), false);
    public static readonly StyledProperty<double> BadgeDiameterProperty =
        AvaloniaProperty.Register<MissionItemIndexBadge, double>(nameof(BadgeDiameter), 22.0);

    static MissionItemIndexBadge()
    {
        AffectsRender<MissionItemIndexBadge>(SequenceIndexProperty, BadgeFillColorProperty,
            MissionItemIsSelectedProperty, BadgeDiameterProperty);
    }

    public int    SequenceIndex  { get => GetValue(SequenceIndexProperty);         set => SetValue(SequenceIndexProperty, value); }
    public Color  BadgeFillColor { get => GetValue(BadgeFillColorProperty);        set => SetValue(BadgeFillColorProperty, value); }
    public bool   IsSelected     { get => GetValue(MissionItemIsSelectedProperty); set => SetValue(MissionItemIsSelectedProperty, value); }
    public double BadgeDiameter  { get => GetValue(BadgeDiameterProperty);         set => SetValue(BadgeDiameterProperty, value); }

    public event EventHandler? BadgeTapped;

    public override void Render(DrawingContext ctx)
    {
        var d  = BadgeDiameter;
        var r  = d / 2.0 - 0.5;
        var cx = Bounds.Width  / 2;
        var cy = Bounds.Height / 2;

        var fill = IsSelected
            ? new SolidColorBrush(QgcColors.ColorOrange)
            : new SolidColorBrush(BadgeFillColor);
        ctx.DrawEllipse(fill, new Pen(new SolidColorBrush(Colors.White), 1.0), new Point(cx, cy), r, r);

        var fs = Math.Min(ScreenMetrics.DefaultFontPixelHeight * 0.85, d * 0.5);
        var ft = new FormattedText(SequenceIndex.ToString(),
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, fs, new SolidColorBrush(Colors.White));
        ctx.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        BadgeTapped?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var d = BadgeDiameter;
        return new Size(d, d);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AltitudeTextField  (#165 — QGC AltitudeFactTextField: altitude + frame selector)
// TemplatedControl combining an altitude value input with an AGL/AMSL/Terrain
// frame-of-reference selector.  Frame defaults drive coordinate interpretation.
// ─────────────────────────────────────────────────────────────────────────────
public enum AltitudeFrame { Relative, Absolute, TerrainFrame }

public sealed class AltitudeTextField : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<double>        AltitudeMetersProperty =
        AvaloniaProperty.Register<AltitudeTextField, double>(nameof(AltitudeMeters), 10.0);
    public static readonly StyledProperty<AltitudeFrame> AltitudeFrameProperty =
        AvaloniaProperty.Register<AltitudeTextField, AltitudeFrame>(nameof(Frame), AltitudeFrame.Relative);
    public static readonly StyledProperty<string>        AltitudeLabelProperty =
        AvaloniaProperty.Register<AltitudeTextField, string>(nameof(AltitudeLabel), "Altitude");
    public static readonly StyledProperty<double>        AltMinValueProperty =
        AvaloniaProperty.Register<AltitudeTextField, double>(nameof(AltMinValue), -1000.0);
    public static readonly StyledProperty<double>        AltMaxValueProperty =
        AvaloniaProperty.Register<AltitudeTextField, double>(nameof(AltMaxValue), 10000.0);

    public double        AltitudeMeters { get => GetValue(AltitudeMetersProperty); set => SetValue(AltitudeMetersProperty, value); }
    public AltitudeFrame Frame          { get => GetValue(AltitudeFrameProperty);  set => SetValue(AltitudeFrameProperty, value); }
    public string        AltitudeLabel  { get => GetValue(AltitudeLabelProperty);  set => SetValue(AltitudeLabelProperty, value); }
    public double        AltMinValue    { get => GetValue(AltMinValueProperty);    set => SetValue(AltMinValueProperty, value); }
    public double        AltMaxValue    { get => GetValue(AltMaxValueProperty);    set => SetValue(AltMaxValueProperty, value); }

    /// <summary>Frame label strings for display ("Rel", "AMSL", "Terrain").</summary>
    public static string FrameLabel(AltitudeFrame f) => f switch
    {
        AltitudeFrame.Absolute     => "AMSL",
        AltitudeFrame.TerrainFrame => "Terrain",
        _                          => "Rel"
    };

    public event EventHandler<double>?        AltitudeChanged;
    public event EventHandler<AltitudeFrame>? FrameChanged;

    public void NotifyAltitudeChanged(double v) => AltitudeChanged?.Invoke(this, v);
    public void NotifyFrameChanged(AltitudeFrame f) => FrameChanged?.Invoke(this, f);
}

// ── #182 CommandArgumentRow ───────────────────────────────────────────────────
public class CommandArgumentRow : Control
{
    public static readonly StyledProperty<string>  CALabelProperty =
        AvaloniaProperty.Register<CommandArgumentRow, string>("CALabel", string.Empty);
    public static readonly StyledProperty<string>  CAValueProperty =
        AvaloniaProperty.Register<CommandArgumentRow, string>("CAValue", string.Empty);
    public static readonly StyledProperty<string>  CAUnitsProperty =
        AvaloniaProperty.Register<CommandArgumentRow, string>("CAUnits", string.Empty);
    public static readonly StyledProperty<bool>    CAIsEditableProperty =
        AvaloniaProperty.Register<CommandArgumentRow, bool>("CAIsEditable", false);
    public static readonly StyledProperty<bool>    CAIsHighlightedProperty =
        AvaloniaProperty.Register<CommandArgumentRow, bool>("CAIsHighlighted", false);

    public string CALabel       { get => GetValue(CALabelProperty);       set => SetValue(CALabelProperty, value); }
    public string CAValue       { get => GetValue(CAValueProperty);       set => SetValue(CAValueProperty, value); }
    public string CAUnits       { get => GetValue(CAUnitsProperty);       set => SetValue(CAUnitsProperty, value); }
    public bool   CAIsEditable  { get => GetValue(CAIsEditableProperty);  set => SetValue(CAIsEditableProperty, value); }
    public bool   CAIsHighlighted{ get => GetValue(CAIsHighlightedProperty); set => SetValue(CAIsHighlightedProperty, value); }

    public event EventHandler? EditRequested;

    static CommandArgumentRow()
    {
        AffectsRender<CommandArgumentRow>(CALabelProperty, CAValueProperty, CAUnitsProperty,
            CAIsEditableProperty, CAIsHighlightedProperty);
    }

    private Rect _editRect;

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // Row background
        var bg = CAIsHighlighted
            ? new SolidColorBrush(Color.FromArgb(30, QgcColors.PrimaryButtonFill.R,
                  QgcColors.PrimaryButtonFill.G, QgcColors.PrimaryButtonFill.B))
            : new SolidColorBrush(QgcColors.Window);
        dc.FillRectangle(bg, new Rect(0, 0, w, h));

        // Bottom separator
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Label (left side, 40% width)
        double labelW = w * 0.40;
        var labelFt = new FormattedText(CALabel, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(labelFt, new Point(8, (h - labelFt.Height) / 2));

        // Value (middle)
        string displayVal = string.IsNullOrEmpty(CAValue) ? "—" : CAValue;
        var valFt = new FormattedText(displayVal, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(valFt, new Point(labelW + 4, (h - valFt.Height) / 2));

        // Units
        if (!string.IsNullOrEmpty(CAUnits))
        {
            var unitFt = new FormattedText(CAUnits, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(unitFt, new Point(labelW + valFt.Width + 6, (h - unitFt.Height) / 2 + 1));
        }

        // Edit button (right edge, only when editable)
        if (CAIsEditable)
        {
            double btnH = h * 0.7;
            double btnW = dfh * 2.5;
            double btnX = w - btnW - 6;
            double btnY = (h - btnH) / 2;
            _editRect = new Rect(btnX, btnY, btnW, btnH);
            double br = ScreenMetrics.DefaultBorderRadius;
            dc.DrawRectangle(new SolidColorBrush(QgcColors.Button),
                new Pen(new SolidColorBrush(QgcColors.GroupBorder)), _editRect, br);
            var editFt = new FormattedText("Edit", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.ButtonText));
            dc.DrawText(editFt, new Point(btnX + (btnW - editFt.Width) / 2, btnY + (btnH - editFt.Height) / 2));
        }
        else
        {
            _editRect = new Rect(-1, -1, 0, 0);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (CAIsEditable && _editRect.Width > 0 && _editRect.Contains(e.GetPosition(this)))
            EditRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : ScreenMetrics.DefaultFontPixelWidth * 30;
        return new Size(w, dfh * 1.8);
    }
}

// ── #212 FactBitmaskView ──────────────────────────────────────────────────────
public class FactBitmaskView : Control
{
    public static readonly StyledProperty<long>                    FBVValueProperty =
        AvaloniaProperty.Register<FactBitmaskView, long>("FBVValue", 0L);
    public static readonly StyledProperty<int>                     FBVBitCountProperty =
        AvaloniaProperty.Register<FactBitmaskView, int>("FBVBitCount", 8);
    public static readonly StyledProperty<IReadOnlyList<string>?>  FBVBitLabelsProperty =
        AvaloniaProperty.Register<FactBitmaskView, IReadOnlyList<string>?>("FBVBitLabels");

    public long                   FBVValue     { get => GetValue(FBVValueProperty);     set => SetValue(FBVValueProperty, value); }
    public int                    FBVBitCount  { get => GetValue(FBVBitCountProperty);  set => SetValue(FBVBitCountProperty, value); }
    public IReadOnlyList<string>? FBVBitLabels { get => GetValue(FBVBitLabelsProperty); set => SetValue(FBVBitLabelsProperty, value); }

    static FactBitmaskView()
    {
        AffectsRender<FactBitmaskView>(FBVValueProperty, FBVBitCountProperty, FBVBitLabelsProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds  = Bounds;
        double w    = bounds.Width;
        double h    = bounds.Height;
        var dfh     = ScreenMetrics.DefaultFontPixelHeight;
        double br   = ScreenMetrics.DefaultBorderRadius;

        int   bits  = Math.Clamp(FBVBitCount, 1, 32);
        long  val   = FBVValue;
        var   labels= FBVBitLabels;

        // Arrange in rows of 8
        int cols    = Math.Min(bits, 8);
        int rows    = (int)Math.Ceiling(bits / 8.0);
        double cellW= (w - 4) / cols;
        double cellH= (h - 4) / rows;

        for (int i = 0; i < bits; i++)
        {
            int col  = i % 8;
            int row  = i / 8;
            bool set = ((val >> i) & 1) == 1;

            double cx = 2 + col * cellW;
            double cy = 2 + row * cellH;

            dc.DrawRectangle(
                new SolidColorBrush(set ? QgcColors.ColorGreen : QgcColors.WindowShade),
                new Pen(new SolidColorBrush(QgcColors.GroupBorder)),
                new Rect(cx, cy, cellW - 2, cellH - 2), br);

            // Bit index
            var idxFt = new FormattedText($"{i}", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.6,
                new SolidColorBrush(set ? QgcColors.ButtonText : QgcColors.TextSecondary));
            dc.DrawText(idxFt, new Point(cx + (cellW - 2 - idxFt.Width) / 2, cy + 1));

            // Label (if provided)
            if (labels != null && i < labels.Count && !string.IsNullOrEmpty(labels[i]))
            {
                var lFt = new FormattedText(labels[i], System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, Typeface.Default, dfh * 0.55,
                    new SolidColorBrush(set ? QgcColors.ButtonText : QgcColors.TextSecondary));
                dc.DrawText(lFt, new Point(cx + (cellW - 2 - lFt.Width) / 2,
                    cy + cellH - 2 - lFt.Height));
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int   bits = Math.Clamp(FBVBitCount, 1, 32);
        int   rows = (int)Math.Ceiling(bits / 8.0);
        var   dfh  = ScreenMetrics.DefaultFontPixelHeight;
        double w   = !double.IsInfinity(availableSize.Width) ? availableSize.Width : dfh * 16;
        return new Size(w, rows * dfh * 2.2);
    }
}

// ── #213 FactPanelRow ─────────────────────────────────────────────────────────
public class FactPanelRow : Control
{
    public static readonly StyledProperty<string> FPRLabelProperty =
        AvaloniaProperty.Register<FactPanelRow, string>("FPRLabel", string.Empty);
    public static readonly StyledProperty<string> FPRValueProperty =
        AvaloniaProperty.Register<FactPanelRow, string>("FPRValue", string.Empty);
    public static readonly StyledProperty<string> FPRUnitProperty =
        AvaloniaProperty.Register<FactPanelRow, string>("FPRUnit", string.Empty);
    public static readonly StyledProperty<bool>   FPRIsEditableProperty =
        AvaloniaProperty.Register<FactPanelRow, bool>("FPRIsEditable", false);

    public string FPRLabel      { get => GetValue(FPRLabelProperty);      set => SetValue(FPRLabelProperty, value); }
    public string FPRValue      { get => GetValue(FPRValueProperty);      set => SetValue(FPRValueProperty, value); }
    public string FPRUnit       { get => GetValue(FPRUnitProperty);       set => SetValue(FPRUnitProperty, value); }
    public bool   FPRIsEditable { get => GetValue(FPRIsEditableProperty); set => SetValue(FPRIsEditableProperty, value); }

    public event EventHandler? EditRequested;

    static FactPanelRow()
    {
        AffectsRender<FactPanelRow>(FPRLabelProperty, FPRValueProperty, FPRUnitProperty, FPRIsEditableProperty);
    }

    private Rect _editBtnRect;

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Label (left 40%)
        var lblFt = new FormattedText(FPRLabel, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.82,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(lblFt, new Point(6, (h - lblFt.Height) / 2));

        // Edit button (right edge, if editable)
        double editBtnW = FPRIsEditable ? dfh * 2.8 : 0;
        double editBtnH = h * 0.65;
        if (FPRIsEditable)
        {
            double ex = w - editBtnW - 4;
            double ey = (h - editBtnH) / 2;
            _editBtnRect = new Rect(ex, ey, editBtnW, editBtnH);
            dc.DrawRectangle(new SolidColorBrush(QgcColors.Button),
                new Pen(new SolidColorBrush(QgcColors.GroupBorder)), _editBtnRect, br);
            var eFt = new FormattedText("Edit", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.ButtonText));
            dc.DrawText(eFt, new Point(ex + (editBtnW - eFt.Width) / 2, ey + (editBtnH - eFt.Height) / 2));
        }
        else
        {
            _editBtnRect = new Rect(-1, -1, 0, 0);
        }

        // Value + unit (center area)
        double valAreaX = w * 0.42;
        double valAreaW = w - valAreaX - editBtnW - 8;
        var valFt = new FormattedText(
            string.IsNullOrEmpty(FPRValue) ? "—" : FPRValue,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.88,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(valFt, new Point(valAreaX, (h - valFt.Height) / 2));

        if (!string.IsNullOrEmpty(FPRUnit))
        {
            var unitFt = new FormattedText(FPRUnit, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(unitFt, new Point(valAreaX + valFt.Width + 4, (h - unitFt.Height) / 2 + 1));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_editBtnRect.Width > 0 && _editBtnRect.Contains(e.GetPosition(this)))
            EditRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : dfh * 22;
        return new Size(w, dfh * 1.8);
    }
}

// ── #222 FactGroupPanel ───────────────────────────────────────────────────────
// Collapsible group header: chevron + group name + item count badge.
// When collapsed renders only the header row; expanded height is set by the host
// (this control only draws the header — child layout is external).
// FGPGroupName, FGPItemCount, FGPIsExpanded.  Raises ToggleRequested on click.
public sealed class FactGroupPanel : Control
{
    public static readonly StyledProperty<string> FGPGroupNameProperty =
        AvaloniaProperty.Register<FactGroupPanel, string>("FGPGroupName", "Group");
    public static readonly StyledProperty<int>    FGPItemCountProperty =
        AvaloniaProperty.Register<FactGroupPanel, int>("FGPItemCount", 0);
    public static readonly StyledProperty<bool>   FGPIsExpandedProperty =
        AvaloniaProperty.Register<FactGroupPanel, bool>("FGPIsExpanded", true);

    static FactGroupPanel()
    {
        AffectsRender<FactGroupPanel>(FGPGroupNameProperty, FGPItemCountProperty, FGPIsExpandedProperty);
    }

    public string FGPGroupName { get => GetValue(FGPGroupNameProperty); set => SetValue(FGPGroupNameProperty, value); }
    public int    FGPItemCount { get => GetValue(FGPItemCountProperty); set => SetValue(FGPItemCountProperty, value); }
    public bool   FGPIsExpanded{ get => GetValue(FGPIsExpandedProperty);set => SetValue(FGPIsExpandedProperty, value); }

    public event EventHandler? ToggleRequested;

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        // Header background
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, new Rect(0, 0, w, h));
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Chevron (▶ collapsed / ▼ expanded)
        double chevSize = dfh * 0.55;
        double chevX    = dfw * 0.6;
        double chevY    = (h - chevSize) / 2;
        var chevGeo = new StreamGeometry();
        using (var ctx = chevGeo.Open())
        {
            if (FGPIsExpanded)
            {
                ctx.BeginFigure(new Point(chevX, chevY), true);
                ctx.LineTo(new Point(chevX + chevSize, chevY));
                ctx.LineTo(new Point(chevX + chevSize / 2, chevY + chevSize));
            }
            else
            {
                ctx.BeginFigure(new Point(chevX, chevY), true);
                ctx.LineTo(new Point(chevX + chevSize, chevY + chevSize / 2));
                ctx.LineTo(new Point(chevX, chevY + chevSize));
            }
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(new SolidColorBrush(QgcColors.TextSecondary), null, chevGeo);

        // Group name
        double textX = chevX + chevSize + dfw * 0.8;
        var nameFt = new FormattedText(FGPGroupName,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 0.88, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(nameFt, new Point(textX, (h - nameFt.Height) / 2));

        // Item count badge (right side)
        if (FGPItemCount > 0)
        {
            string badge = FGPItemCount.ToString();
            var badgeFt = new FormattedText(badge,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.7, new SolidColorBrush(QgcColors.ButtonText));
            double badgeW = Math.Max(badgeFt.Width + 8, 20);
            double badgeH = dfh * 0.9;
            double badgeX = w - badgeW - dfw * 0.5;
            double badgeY = (h - badgeH) / 2;
            dc.DrawRectangle(new SolidColorBrush(QgcColors.ColorGrey), null,
                new Rect(badgeX, badgeY, badgeW, badgeH),
                badgeH / 2, badgeH / 2);
            dc.DrawText(badgeFt, new Point(badgeX + (badgeW - badgeFt.Width) / 2,
                badgeY + (badgeH - badgeFt.Height) / 2));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        FGPIsExpanded = !FGPIsExpanded;
        ToggleRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width) ? available.Width : 300;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}

// ── #223 UnitConversionLabel ──────────────────────────────────────────────────
// Displays a numeric value with auto metric/imperial unit conversion.
// UCLValue (raw SI value), UCLSiUnit (e.g. "m", "m/s", "°C"),
// UCLUseImperial toggles the conversion.  The converted value + unit string
// is rendered right-aligned; an optional UCLLabel is left-aligned.
public sealed class UnitConversionLabel : Control
{
    public static readonly StyledProperty<double> UCLValueProperty =
        AvaloniaProperty.Register<UnitConversionLabel, double>("UCLValue", 0.0);
    public static readonly StyledProperty<string> UCLSiUnitProperty =
        AvaloniaProperty.Register<UnitConversionLabel, string>("UCLSiUnit", "m");
    public static readonly StyledProperty<bool>   UCLUseImperialProperty =
        AvaloniaProperty.Register<UnitConversionLabel, bool>("UCLUseImperial", false);
    public static readonly StyledProperty<string> UCLLabelProperty =
        AvaloniaProperty.Register<UnitConversionLabel, string>("UCLLabel", string.Empty);

    static UnitConversionLabel()
    {
        AffectsRender<UnitConversionLabel>(UCLValueProperty, UCLSiUnitProperty,
                                           UCLUseImperialProperty, UCLLabelProperty);
    }

    public double UCLValue      { get => GetValue(UCLValueProperty);      set => SetValue(UCLValueProperty, value); }
    public string UCLSiUnit     { get => GetValue(UCLSiUnitProperty);     set => SetValue(UCLSiUnitProperty, value); }
    public bool   UCLUseImperial{ get => GetValue(UCLUseImperialProperty);set => SetValue(UCLUseImperialProperty, value); }
    public string UCLLabel      { get => GetValue(UCLLabelProperty);      set => SetValue(UCLLabelProperty, value); }

    private (double ConvertedValue, string DisplayUnit) Convert()
    {
        if (!UCLUseImperial) return (UCLValue, UCLSiUnit);
        return UCLSiUnit switch
        {
            "m"     => (UCLValue * 3.28084,  "ft"),
            "km"    => (UCLValue * 0.621371, "mi"),
            "m/s"   => (UCLValue * 1.94384,  "kn"),
            "km/h"  => (UCLValue * 0.621371, "mph"),
            "°C"    => (UCLValue * 9.0 / 5.0 + 32, "°F"),
            "kg"    => (UCLValue * 2.20462,  "lb"),
            _       => (UCLValue, UCLSiUnit)
        };
    }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        var (cv, unit) = Convert();
        string valStr  = $"{cv:G5}";

        // Optional left label
        if (!string.IsNullOrEmpty(UCLLabel))
        {
            var lbFt = new FormattedText(UCLLabel,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(lbFt, new Point(dfw * 0.3, (h - lbFt.Height) / 2));
        }

        // Value (right-aligned)
        var valFt = new FormattedText(valStr,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 0.9, new SolidColorBrush(QgcColors.Text));

        // Unit suffix (smaller, right of value)
        var unitFt = new FormattedText(unit,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.7, new SolidColorBrush(QgcColors.TextSecondary));

        double totalW = valFt.Width + dfw * 0.3 + unitFt.Width;
        double startX = w - totalW - dfw * 0.3;
        double midY   = (h - valFt.Height) / 2;
        dc.DrawText(valFt,  new Point(startX, midY));
        dc.DrawText(unitFt, new Point(startX + valFt.Width + dfw * 0.3,
                                      midY + valFt.Height - unitFt.Height));
    }

    protected override Size MeasureOverride(Size available)
    {
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w   = !double.IsInfinity(available.Width) ? available.Width : dfh * 14;
        return new Size(w, dfh * 1.6);
    }
}

// ── #233 FactTableView ────────────────────────────────────────────────────────
// Tabular multi-row display of fact name/value/unit triples.
// FTVRows is IReadOnlyList<(string Name, string Value, string Unit)>.
// Renders a header row + alternating-background data rows with 3 columns.
// Column widths: Name 45%, Value 35%, Unit 20%.
public sealed class FactTableView : Control
{
    public static readonly StyledProperty<IReadOnlyList<(string Name, string Value, string Unit)>?>
        FTVRowsProperty =
        AvaloniaProperty.Register<FactTableView,
            IReadOnlyList<(string Name, string Value, string Unit)>?>("FTVRows", null);
    public static readonly StyledProperty<string> FTVTitleProperty =
        AvaloniaProperty.Register<FactTableView, string>("FTVTitle", string.Empty);

    static FactTableView()
    {
        AffectsRender<FactTableView>(FTVRowsProperty, FTVTitleProperty);
        AffectsMeasure<FactTableView>(FTVRowsProperty);
    }

    public IReadOnlyList<(string Name, string Value, string Unit)>? FTVRows
    {
        get => GetValue(FTVRowsProperty);
        set => SetValue(FTVRowsProperty, value);
    }
    public string FTVTitle { get => GetValue(FTVTitleProperty); set => SetValue(FTVTitleProperty, value); }

    public override void Render(DrawingContext dc)
    {
        var rows = FTVRows;
        double w    = Bounds.Width;
        double dfh  = ScreenMetrics.DefaultFontPixelHeight;
        double dfw  = ScreenMetrics.DefaultFontPixelWidth;
        double rowH = dfh * 1.8;
        double y    = 0;

        // Optional title header
        if (!string.IsNullOrEmpty(FTVTitle))
        {
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, new Rect(0, y, w, rowH));
            var titleFt = new FormattedText(FTVTitle,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
                dfh * 0.85, new SolidColorBrush(QgcColors.Text));
            dc.DrawText(titleFt, new Point(dfw * 0.5, y + (rowH - titleFt.Height) / 2));
            y += rowH;
            dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
                new Point(0, y - 0.5), new Point(w, y - 0.5));
        }

        if (rows == null) return;

        double col0W = w * 0.45;
        double col1W = w * 0.35;
        double col2X = col0W + col1W;

        for (int i = 0; i < rows.Count; i++)
        {
            var (name, val, unit) = rows[i];
            double rowY = y + i * rowH;

            if (i % 2 == 1)
                dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
                    new Rect(0, rowY, w, rowH));

            var nameFt = new FormattedText(name,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.TextSecondary))
            { MaxTextWidth = col0W - dfw };
            dc.DrawText(nameFt, new Point(dfw * 0.4, rowY + (rowH - nameFt.Height) / 2));

            var valFt = new FormattedText(val,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
                dfh * 0.85, new SolidColorBrush(QgcColors.Text));
            dc.DrawText(valFt, new Point(col0W + dfw * 0.3, rowY + (rowH - valFt.Height) / 2));

            var unitFt = new FormattedText(unit,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.72, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(unitFt, new Point(col2X + dfw * 0.2, rowY + (rowH - unitFt.Height) / 2));

            dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
                new Point(0, rowY + rowH - 0.5), new Point(w, rowY + rowH - 0.5));
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        double dfh  = ScreenMetrics.DefaultFontPixelHeight;
        double rowH = dfh * 1.8;
        int    cnt  = FTVRows?.Count ?? 0;
        double titleH = string.IsNullOrEmpty(FTVTitle) ? 0 : rowH;
        double w = !double.IsInfinity(available.Width) ? available.Width : 320;
        return new Size(w, titleH + cnt * rowH);
    }
}

// ── #234 QGCProgressBar ───────────────────────────────────────────────────────
// Labelled horizontal progress bar with optional percent text overlay.
// QPBValue 0.0–1.0, QPBLabel shown left, QPBShowPercent toggles "XX%" overlay.
// QPBFillColor lets callers override the default blue fill.
public sealed class QGCProgressBar : Control
{
    public static readonly StyledProperty<double> QPBValueProperty =
        AvaloniaProperty.Register<QGCProgressBar, double>("QPBValue", 0.0);
    public static readonly StyledProperty<string> QPBLabelProperty =
        AvaloniaProperty.Register<QGCProgressBar, string>("QPBLabel", string.Empty);
    public static readonly StyledProperty<bool>   QPBShowPercentProperty =
        AvaloniaProperty.Register<QGCProgressBar, bool>("QPBShowPercent", true);
    public static readonly StyledProperty<Color>  QPBFillColorProperty =
        AvaloniaProperty.Register<QGCProgressBar, Color>("QPBFillColor", Color.FromRgb(0, 140, 220));

    static QGCProgressBar()
    {
        AffectsRender<QGCProgressBar>(QPBValueProperty, QPBLabelProperty,
                                      QPBShowPercentProperty, QPBFillColorProperty);
        AffectsMeasure<QGCProgressBar>(QPBLabelProperty);
    }

    public double QPBValue      { get => GetValue(QPBValueProperty);      set => SetValue(QPBValueProperty, value); }
    public string QPBLabel      { get => GetValue(QPBLabelProperty);      set => SetValue(QPBLabelProperty, value); }
    public bool   QPBShowPercent{ get => GetValue(QPBShowPercentProperty);set => SetValue(QPBShowPercentProperty, value); }
    public Color  QPBFillColor  { get => GetValue(QPBFillColorProperty);  set => SetValue(QPBFillColorProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;
        double br  = ScreenMetrics.DefaultBorderRadius;

        double labelW = 0;
        if (!string.IsNullOrEmpty(QPBLabel))
        {
            var lbFt = new FormattedText(QPBLabel,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(lbFt, new Point(0, (h - lbFt.Height) / 2));
            labelW = lbFt.Width + dfw;
        }

        double barX = labelW;
        double barW = w - labelW;
        double barH = Math.Min(h * 0.55, 12);
        double barY = (h - barH) / 2;

        // Track
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
            new Rect(barX, barY, barW, barH), br, br);

        // Fill
        double fillW = Math.Clamp(QPBValue, 0.0, 1.0) * barW;
        if (fillW > 0)
            dc.DrawRectangle(new SolidColorBrush(QPBFillColor), null,
                new Rect(barX, barY, fillW, barH), br, br);

        // Percent overlay
        if (QPBShowPercent)
        {
            string pct = $"{QPBValue * 100:F0}%";
            var pFt = new FormattedText(pct,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.72, new SolidColorBrush(Colors.White));
            dc.DrawText(pFt, new Point(barX + (barW - pFt.Width) / 2, barY + (barH - pFt.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w   = !double.IsInfinity(available.Width) ? available.Width : 240;
        return new Size(w, dfh * 1.6);
    }
}

// ── #235 StatusTextLabel ──────────────────────────────────────────────────────
// Coloured badge showing a status string.  STLSeverity maps to colours:
// 0=Emergency/1=Alert/2=Critical → red, 3=Error → orange, 4=Warning → yellow,
// 5=Notice/6=Info → blue, 7=Debug → grey.  STLText is the message.
public sealed class StatusTextLabel : Control
{
    public static readonly StyledProperty<string> STLTextProperty =
        AvaloniaProperty.Register<StatusTextLabel, string>("STLText", string.Empty);
    public static readonly StyledProperty<int>    STLSeverityProperty =
        AvaloniaProperty.Register<StatusTextLabel, int>("STLSeverity", 6);

    static StatusTextLabel()
    {
        AffectsRender<StatusTextLabel>(STLTextProperty, STLSeverityProperty);
        AffectsMeasure<StatusTextLabel>(STLTextProperty);
    }

    public string STLText     { get => GetValue(STLTextProperty);     set => SetValue(STLTextProperty, value); }
    public int    STLSeverity { get => GetValue(STLSeverityProperty); set => SetValue(STLSeverityProperty, value); }

    private Color SeverityColor => STLSeverity switch
    {
        <= 2 => QgcColors.ColorRed,
        3    => QgcColors.ColorOrange,
        4    => Color.FromRgb(200, 180, 0),
        <= 6 => QgcColors.ColorBlue,
        _    => QgcColors.ColorGrey
    };

    public override void Render(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(STLText)) return;
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = 5;
        double br  = h / 2;

        Color  badgeC = SeverityColor;
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, badgeC.R, badgeC.G, badgeC.B)),
            new Pen(new SolidColorBrush(badgeC), 0.75),
            new Rect(0, 0, w, h), br, br);

        var ft = new FormattedText(STLText,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.78, new SolidColorBrush(badgeC))
        { MaxTextWidth = w - pad * 2 };
        dc.DrawText(ft, new Point(pad, (h - ft.Height) / 2));
    }

    protected override Size MeasureOverride(Size available)
    {
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = 5;
        double maxW = !double.IsInfinity(available.Width) ? available.Width : 300;
        var ft = new FormattedText(STLText,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.78, new SolidColorBrush(Colors.White))
        { MaxTextWidth = maxW - pad * 2 };
        return new Size(Math.Min(ft.Width + pad * 2, maxW), dfh * 1.5);
    }
}

