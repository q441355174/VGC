using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Collections.ObjectModel;
using System.Globalization;

namespace VGC.Views.Controls;

// ---------------------------------------------------------------------------
// 1. RCChannelMonitor
// QGC equivalent: QmlControls/RCChannelMonitor.qml
// Shows 4 horizontal bars (Roll/Pitch/Throttle/Yaw) with real-time PWM values.
// ---------------------------------------------------------------------------

/// <summary>
/// Displays RC channel bars with real-time PWM values (1000-2000 range).
/// Each channel renders as: label + horizontal bar + numeric value.
/// Bar fill is <see cref="QgcColors.ColorBlue"/> when within normal range,
/// <see cref="QgcColors.ColorOrange"/> when near the PWM edges.
/// </summary>
public sealed class RCChannelMonitor : Control
{
    private const double ChannelHeight = 30;
    private const double LabelWidth = 70;
    private const double ValueWidth = 50;
    private const double EdgeThreshold = 50; // PWM units from min/max considered "edge"

    public static readonly StyledProperty<IReadOnlyList<RCChannelValue>> ChannelsProperty =
        AvaloniaProperty.Register<RCChannelMonitor, IReadOnlyList<RCChannelValue>>(
            nameof(Channels), Array.Empty<RCChannelValue>());

    static RCChannelMonitor()
    {
        AffectsRender<RCChannelMonitor>(ChannelsProperty);
        AffectsMeasure<RCChannelMonitor>(ChannelsProperty);
    }

    public IReadOnlyList<RCChannelValue> Channels
    {
        get => GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 20 || bounds.Height < 10) return;

        var channels = Channels;
        if (channels.Count == 0) return;

        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var boldTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var trackBrush = new SolidColorBrush(QgcColors.WindowShade);
        var blueBrush = new SolidColorBrush(QgcColors.ColorBlue);
        var orangeBrush = new SolidColorBrush(QgcColors.ColorOrange);
        var borderPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);

        var barLeft = LabelWidth + ScreenMetrics.StandardMargin;
        var barRight = bounds.Width - ValueWidth - ScreenMetrics.StandardMargin;
        var barWidth = barRight - barLeft;

        for (var i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            var y = i * ChannelHeight;
            var barY = y + 6;
            var barH = ChannelHeight - 12;

            // Label
            var label = new FormattedText(ch.Name, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(label, new Point(ScreenMetrics.StandardMargin, y + (ChannelHeight - label.Height) / 2));

            // Track background
            context.DrawRectangle(trackBrush, borderPen,
                new Rect(barLeft, barY, barWidth, barH),
                ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

            // Fill bar
            var range = ch.Max - ch.Min;
            if (range > 0)
            {
                var fraction = Math.Clamp((ch.Value - ch.Min) / (double)range, 0, 1);
                var fillWidth = barWidth * fraction;

                var nearEdge = ch.Value <= ch.Min + EdgeThreshold || ch.Value >= ch.Max - EdgeThreshold;
                var fillBrush = nearEdge ? orangeBrush : blueBrush;

                context.DrawRectangle(fillBrush, null,
                    new Rect(barLeft, barY, fillWidth, barH),
                    ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);
            }

            // Value text
            var valueStr = ch.Value.ToString(CultureInfo.InvariantCulture);
            var valueText = new FormattedText(valueStr, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, boldTypeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(valueText, new Point(barRight + ScreenMetrics.StandardMargin,
                y + (ChannelHeight - valueText.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        var h = Channels.Count * ChannelHeight;
        return new Size(w, h);
    }
}

// ---------------------------------------------------------------------------
// 2. AxisMonitor
// QGC equivalent: QmlControls/AxisMonitor.qml
// Single axis bar showing value (-1 to +1) with deadband zone.
// ---------------------------------------------------------------------------

/// <summary>
/// Single-axis center-zero bar with optional deadband zone.
/// Value range is -1..+1. Deadband zone is drawn as a gray region around center.
/// Fill extends from center toward the current value in <see cref="QgcColors.ColorBlue"/>.
/// </summary>
public sealed class AxisMonitor : Control
{
    private const double BarHeight = 24;
    private const double LabelWidth = 60;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AxisMonitor, double>(nameof(Value));

    public static readonly StyledProperty<double> DeadbandProperty =
        AvaloniaProperty.Register<AxisMonitor, double>(nameof(Deadband));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AxisMonitor, string>(nameof(Label), "");

    static AxisMonitor()
    {
        AffectsRender<AxisMonitor>(ValueProperty, DeadbandProperty, LabelProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, -1, 1));
    }

    public double Deadband
    {
        get => GetValue(DeadbandProperty);
        set => SetValue(DeadbandProperty, Math.Clamp(value, 0, 0.2));
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 40 || bounds.Height < 10) return;

        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var trackBrush = new SolidColorBrush(QgcColors.WindowShade);
        var deadbandBrush = new SolidColorBrush(QgcColors.ColorGrey);
        var fillBrush = new SolidColorBrush(QgcColors.ColorBlue);
        var borderPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
        var centerPen = new Pen(new SolidColorBrush(QgcColors.Text), 1);

        // Label
        if (!string.IsNullOrEmpty(Label))
        {
            var labelText = new FormattedText(Label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(labelText, new Point(ScreenMetrics.StandardMargin, (BarHeight - labelText.Height) / 2));
        }

        var barLeft = string.IsNullOrEmpty(Label) ? ScreenMetrics.StandardMargin : LabelWidth;
        var barWidth = bounds.Width - barLeft - ScreenMetrics.StandardMargin;
        var barY = 0.0;
        var barH = BarHeight;
        var centerX = barLeft + barWidth / 2;

        // Track background
        context.DrawRectangle(trackBrush, borderPen,
            new Rect(barLeft, barY, barWidth, barH),
            ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

        // Deadband zone (gray region around center)
        if (Deadband > 0)
        {
            var dbHalfWidth = barWidth / 2 * Deadband;
            var dbBrush = new SolidColorBrush(Color.FromArgb(80, QgcColors.ColorGrey.R, QgcColors.ColorGrey.G, QgcColors.ColorGrey.B));
            context.DrawRectangle(dbBrush, null,
                new Rect(centerX - dbHalfWidth, barY, dbHalfWidth * 2, barH));
        }

        // Fill from center toward value
        var clampedValue = Math.Clamp(Value, -1, 1);
        var fillHalfWidth = barWidth / 2;
        if (Math.Abs(clampedValue) > 0.001)
        {
            double fillX, fillW;
            if (clampedValue > 0)
            {
                fillX = centerX;
                fillW = fillHalfWidth * clampedValue;
            }
            else
            {
                fillW = fillHalfWidth * -clampedValue;
                fillX = centerX - fillW;
            }
            context.DrawRectangle(fillBrush, null, new Rect(fillX, barY + 1, fillW, barH - 2));
        }

        // Center line
        context.DrawLine(centerPen, new Point(centerX, barY), new Point(centerX, barY + barH));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        return new Size(w, BarHeight);
    }
}

// ---------------------------------------------------------------------------
// 3. AirframeSelectionGrid
// QGC equivalent: AutoPilotPlugins/APMAirframeComponent.qml
// Grid of airframe cards with icon placeholder + title.
// ---------------------------------------------------------------------------

/// <summary>
/// Grid of selectable airframe cards. Each card shows a title and icon area.
/// The selected card is highlighted with <see cref="QgcColors.ButtonHighlight"/>.
/// Columns are auto-calculated from available width.
/// </summary>
public sealed class AirframeSelectionGrid : Control
{
    private const double CardWidth = 200;
    private const double CardHeight = 180;
    private const double CardSpacing = 8;

    public static readonly StyledProperty<IReadOnlyList<AirframeEntry>> AirframesProperty =
        AvaloniaProperty.Register<AirframeSelectionGrid, IReadOnlyList<AirframeEntry>>(
            nameof(Airframes), Array.Empty<AirframeEntry>());

    public static readonly StyledProperty<AirframeType> SelectedTypeProperty =
        AvaloniaProperty.Register<AirframeSelectionGrid, AirframeType>(nameof(SelectedType));

    static AirframeSelectionGrid()
    {
        AffectsRender<AirframeSelectionGrid>(AirframesProperty, SelectedTypeProperty);
        AffectsMeasure<AirframeSelectionGrid>(AirframesProperty);
    }

    public IReadOnlyList<AirframeEntry> Airframes
    {
        get => GetValue(AirframesProperty);
        set => SetValue(AirframesProperty, value);
    }

    public AirframeType SelectedType
    {
        get => GetValue(SelectedTypeProperty);
        set => SetValue(SelectedTypeProperty, value);
    }

    public event EventHandler<AirframeEntry>? AirframeSelected;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < CardWidth || bounds.Height < 10) return;

        var airframes = Airframes;
        if (airframes.Count == 0) return;

        var columns = Math.Max(1, (int)((bounds.Width + CardSpacing) / (CardWidth + CardSpacing)));
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var subtypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var borderPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
        var selectedPen = new Pen(new SolidColorBrush(QgcColors.ButtonHighlight), 2);

        for (var i = 0; i < airframes.Count; i++)
        {
            var af = airframes[i];
            var col = i % columns;
            var row = i / columns;
            var x = col * (CardWidth + CardSpacing);
            var y = row * (CardHeight + CardSpacing);

            if (y + CardHeight > bounds.Height + CardHeight) break; // off screen

            var isSelected = af.Type == SelectedType;
            var bgBrush = new SolidColorBrush(isSelected ? QgcColors.ButtonHighlight : QgcColors.WindowShade);
            var pen = isSelected ? selectedPen : borderPen;

            // Card background
            context.DrawRectangle(bgBrush, pen,
                new Rect(x, y, CardWidth, CardHeight),
                ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

            // Title text at top
            var textColor = isSelected ? QgcColors.ButtonHighlightText : QgcColors.Text;
            var titleText = new FormattedText(af.Name, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.MediumFontPointSize,
                new SolidColorBrush(textColor));
            context.DrawText(titleText, new Point(x + (CardWidth - titleText.Width) / 2, y + 10));

            // Icon placeholder area (centered rectangle)
            var iconSize = 80.0;
            var iconX = x + (CardWidth - iconSize) / 2;
            var iconY = y + 45;
            var iconBgColor = isSelected
                ? Color.FromArgb(60, 0, 0, 0)
                : QgcColors.WindowShadeDark;
            var iconBrush = new SolidColorBrush(iconBgColor);
            context.DrawRectangle(iconBrush, null,
                new Rect(iconX, iconY, iconSize, iconSize),
                ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

            // Placeholder icon text
            var iconLabel = af.IconKey ?? af.Type.ToString();
            var iconText = new FormattedText(iconLabel, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, subtypeface, ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(isSelected ? QgcColors.ButtonHighlightText : QgcColors.TextSecondary));
            context.DrawText(iconText, new Point(
                iconX + (iconSize - iconText.Width) / 2,
                iconY + (iconSize - iconText.Height) / 2));

            // Type label at bottom
            var typeLabel = new FormattedText(af.Type.ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, subtypeface, ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(isSelected ? QgcColors.ButtonHighlightText : QgcColors.TextSecondary));
            context.DrawText(typeLabel, new Point(
                x + (CardWidth - typeLabel.Width) / 2,
                y + CardHeight - typeLabel.Height - 8));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var columns = Math.Max(1, (int)((Bounds.Width + CardSpacing) / (CardWidth + CardSpacing)));
        var col = (int)(pos.X / (CardWidth + CardSpacing));
        var row = (int)(pos.Y / (CardHeight + CardSpacing));

        // Verify click is inside the card, not in spacing
        var cardX = col * (CardWidth + CardSpacing);
        var cardY = row * (CardHeight + CardSpacing);
        if (pos.X < cardX || pos.X > cardX + CardWidth) return;
        if (pos.Y < cardY || pos.Y > cardY + CardHeight) return;

        var index = row * columns + col;
        var airframes = Airframes;
        if (index >= 0 && index < airframes.Count)
        {
            SelectedType = airframes[index].Type;
            AirframeSelected?.Invoke(this, airframes[index]);
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        var count = Airframes.Count;
        if (count == 0) return new Size(w, 0);

        var columns = Math.Max(1, (int)((w + CardSpacing) / (CardWidth + CardSpacing)));
        var rows = (count + columns - 1) / columns;
        var h = rows * (CardHeight + CardSpacing) - CardSpacing;
        return new Size(w, h);
    }
}

// ---------------------------------------------------------------------------
// 4. MotorTestPanel
// QGC equivalent: AutoPilotPlugins/Common/MotorComponent.qml
// Safety switch + throttle slider + motor buttons row.
// ---------------------------------------------------------------------------

/// <summary>
/// Motor test panel with safety toggle, throttle slider (with tick marks), and
/// numbered motor buttons plus All and Stop buttons.
/// </summary>
public sealed class MotorTestPanel : Control
{
    private const double SwitchRowHeight = 40;
    private const double SliderHeight = 50;
    private const double ButtonRowHeight = 44;
    private const double ButtonWidth = 44;
    private const double ButtonSpacing = 6;
    private const double TotalTopPadding = 10;

    public static readonly StyledProperty<bool> SafetyEnabledProperty =
        AvaloniaProperty.Register<MotorTestPanel, bool>(nameof(SafetyEnabled), true);

    public static readonly StyledProperty<double> ThrottlePercentProperty =
        AvaloniaProperty.Register<MotorTestPanel, double>(nameof(ThrottlePercent));

    public static readonly StyledProperty<int> MotorCountProperty =
        AvaloniaProperty.Register<MotorTestPanel, int>(nameof(MotorCount), 4);

    static MotorTestPanel()
    {
        AffectsRender<MotorTestPanel>(SafetyEnabledProperty, ThrottlePercentProperty, MotorCountProperty);
        AffectsMeasure<MotorTestPanel>(MotorCountProperty);
    }

    public bool SafetyEnabled
    {
        get => GetValue(SafetyEnabledProperty);
        set => SetValue(SafetyEnabledProperty, value);
    }

    public double ThrottlePercent
    {
        get => GetValue(ThrottlePercentProperty);
        set => SetValue(ThrottlePercentProperty, Math.Clamp(value, 0, 100));
    }

    public int MotorCount
    {
        get => GetValue(MotorCountProperty);
        set => SetValue(MotorCountProperty, Math.Max(1, value));
    }

    public event EventHandler? SafetyToggled;
    public event EventHandler<double>? ThrottleChanged;
    public event EventHandler<int>? MotorTestRequested;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 100 || bounds.Height < 60) return;

        var w = bounds.Width;
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var boldTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var margin = ScreenMetrics.LayoutMargin;

        // ---- Safety switch row ----
        var switchY = TotalTopPadding;
        var safetyLabel = new FormattedText("Safety Switch", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
            new SolidColorBrush(QgcColors.Text));
        context.DrawText(safetyLabel, new Point(margin, switchY + (SwitchRowHeight - safetyLabel.Height) / 2));

        // Toggle switch visual
        var switchWidth = 50.0;
        var switchHeight = 26.0;
        var switchX = w - margin - switchWidth;
        var switchCenterY = switchY + (SwitchRowHeight - switchHeight) / 2;
        var switchRadius = switchHeight / 2;

        var switchBg = SafetyEnabled
            ? new SolidColorBrush(QgcColors.ColorGreen)
            : new SolidColorBrush(QgcColors.WindowShade);
        var switchBorder = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
        context.DrawRectangle(switchBg, switchBorder,
            new Rect(switchX, switchCenterY, switchWidth, switchHeight),
            switchRadius, switchRadius);

        // Toggle thumb
        var thumbX = SafetyEnabled ? switchX + switchWidth - switchHeight + 2 : switchX + 2;
        context.DrawEllipse(new SolidColorBrush(QgcColors.ButtonText), null,
            new Point(thumbX + switchHeight / 2 - 2, switchCenterY + switchHeight / 2),
            switchHeight / 2 - 4, switchHeight / 2 - 4);

        // ---- Throttle slider ----
        var sliderY = switchY + SwitchRowHeight + margin;
        var throttleLabel = new FormattedText($"Throttle: {ThrottlePercent:F0}%", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
            new SolidColorBrush(QgcColors.Text));
        context.DrawText(throttleLabel, new Point(margin, sliderY));

        var trackY = sliderY + throttleLabel.Height + 4;
        var trackH = 10.0;
        var trackLeft = margin;
        var trackRight = w - margin;
        var trackW = trackRight - trackLeft;
        var trackBrush = new SolidColorBrush(QgcColors.WindowShade);
        var trackBorderPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
        context.DrawRectangle(trackBrush, trackBorderPen,
            new Rect(trackLeft, trackY, trackW, trackH),
            trackH / 2, trackH / 2);

        // Fill
        var fillFraction = ThrottlePercent / 100.0;
        var fillW = trackW * fillFraction;
        if (fillW > 0)
        {
            context.DrawRectangle(new SolidColorBrush(QgcColors.ColorBlue), null,
                new Rect(trackLeft, trackY, fillW, trackH),
                trackH / 2, trackH / 2);
        }

        // Tick marks
        var tickPen = new Pen(new SolidColorBrush(QgcColors.TextSecondary), 1);
        for (var pct = 0; pct <= 100; pct += 25)
        {
            var tickX = trackLeft + trackW * pct / 100.0;
            context.DrawLine(tickPen, new Point(tickX, trackY + trackH + 2), new Point(tickX, trackY + trackH + 8));

            var tickLabel = new FormattedText($"{pct}", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(QgcColors.TextSecondary));
            context.DrawText(tickLabel, new Point(tickX - tickLabel.Width / 2, trackY + trackH + 10));
        }

        // Thumb
        var thumbPosX = trackLeft + trackW * fillFraction;
        context.DrawEllipse(new SolidColorBrush(QgcColors.ButtonHighlight), null,
            new Point(thumbPosX, trackY + trackH / 2), 8, 8);

        // ---- Motor buttons row ----
        var buttonY = sliderY + SliderHeight + margin + 10;
        var totalButtons = MotorCount + 2; // numbered + All + Stop
        var buttonRowWidth = totalButtons * (ButtonWidth + ButtonSpacing) - ButtonSpacing;
        var buttonStartX = (w - buttonRowWidth) / 2;

        for (var i = 0; i < totalButtons; i++)
        {
            var bx = buttonStartX + i * (ButtonWidth + ButtonSpacing);
            string label;
            Color bgColor;
            Color textColor;

            if (i < MotorCount)
            {
                label = (i + 1).ToString();
                bgColor = QgcColors.Button;
                textColor = QgcColors.ButtonText;
            }
            else if (i == MotorCount)
            {
                label = "All";
                bgColor = QgcColors.PrimaryButton;
                textColor = QgcColors.PrimaryButtonText;
            }
            else
            {
                label = "Stop";
                bgColor = QgcColors.ColorRed;
                textColor = QgcColors.ButtonText;
            }

            var bgBrush = new SolidColorBrush(bgColor);
            var btnPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
            context.DrawRectangle(bgBrush, btnPen,
                new Rect(bx, buttonY, ButtonWidth, ButtonRowHeight),
                ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

            var btnText = new FormattedText(label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, boldTypeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(textColor));
            context.DrawText(btnText, new Point(
                bx + (ButtonWidth - btnText.Width) / 2,
                buttonY + (ButtonRowHeight - btnText.Height) / 2));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var w = Bounds.Width;
        var margin = ScreenMetrics.LayoutMargin;

        // Safety switch hit test
        var switchY = TotalTopPadding;
        var switchWidth = 50.0;
        var switchHeight = 26.0;
        var switchX = w - margin - switchWidth;
        var switchCenterY = switchY + (SwitchRowHeight - switchHeight) / 2;
        if (pos.X >= switchX && pos.X <= switchX + switchWidth &&
            pos.Y >= switchCenterY && pos.Y <= switchCenterY + switchHeight)
        {
            SafetyEnabled = !SafetyEnabled;
            SafetyToggled?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // Throttle slider hit test
        var sliderY = switchY + SwitchRowHeight + margin;
        var trackLeft = margin;
        var trackRight = w - margin;
        var trackW = trackRight - trackLeft;
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var throttleLabel = new FormattedText("Throttle: 100%", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
            new SolidColorBrush(QgcColors.Text));
        var trackY = sliderY + throttleLabel.Height + 4;
        var trackH = 10.0;
        if (pos.Y >= trackY - 10 && pos.Y <= trackY + trackH + 10 &&
            pos.X >= trackLeft && pos.X <= trackRight)
        {
            ThrottlePercent = Math.Clamp((pos.X - trackLeft) / trackW * 100, 0, 100);
            ThrottleChanged?.Invoke(this, ThrottlePercent);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // Motor button hit test
        var buttonY = sliderY + SliderHeight + margin + 10;
        var totalButtons = MotorCount + 2;
        var buttonRowWidth = totalButtons * (ButtonWidth + ButtonSpacing) - ButtonSpacing;
        var buttonStartX = (w - buttonRowWidth) / 2;

        if (pos.Y >= buttonY && pos.Y <= buttonY + ButtonRowHeight)
        {
            for (var i = 0; i < totalButtons; i++)
            {
                var bx = buttonStartX + i * (ButtonWidth + ButtonSpacing);
                if (pos.X >= bx && pos.X <= bx + ButtonWidth)
                {
                    if (i < MotorCount)
                        MotorTestRequested?.Invoke(this, i);
                    else if (i == MotorCount)
                        MotorTestRequested?.Invoke(this, -1); // All
                    else
                        MotorTestRequested?.Invoke(this, -2); // Stop

                    e.Handled = true;
                    return;
                }
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        var h = TotalTopPadding + SwitchRowHeight + ScreenMetrics.LayoutMargin +
                SliderHeight + ScreenMetrics.LayoutMargin + 10 + ButtonRowHeight + 30;
        return new Size(w, h);
    }
}

// ---------------------------------------------------------------------------
// 5. PidTuningPanel
// QGC equivalent: AutoPilotPlugins/APMTuningComponent.qml
// Group of labeled sliders for PID parameters.
// ---------------------------------------------------------------------------

/// <summary>
/// Displays a vertical list of PID parameter sliders.
/// Each row has a fixed-width label, a slider track, and a value readout.
/// </summary>
public sealed class PidTuningPanel : Control
{
    private const double RowHeight = 36;
    private const double LabelColumnWidth = 120;
    private const double ValueColumnWidth = 60;

    public static readonly StyledProperty<IReadOnlyList<PidSliderParam>> ParametersProperty =
        AvaloniaProperty.Register<PidTuningPanel, IReadOnlyList<PidSliderParam>>(
            nameof(Parameters), Array.Empty<PidSliderParam>());

    static PidTuningPanel()
    {
        AffectsRender<PidTuningPanel>(ParametersProperty);
        AffectsMeasure<PidTuningPanel>(ParametersProperty);
    }

    public IReadOnlyList<PidSliderParam> Parameters
    {
        get => GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    public event EventHandler<(string Name, double Value)>? ParameterChanged;

    // Mutable copy for tracking drag edits
    private double[]? _editValues;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 100 || bounds.Height < 10) return;

        var parameters = Parameters;
        if (parameters.Count == 0) return;

        // Ensure edit values array matches
        if (_editValues == null || _editValues.Length != parameters.Count)
        {
            _editValues = new double[parameters.Count];
            for (var i = 0; i < parameters.Count; i++)
                _editValues[i] = parameters[i].Value;
        }

        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var boldTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var trackBrush = new SolidColorBrush(QgcColors.WindowShade);
        var fillBrush = new SolidColorBrush(QgcColors.ColorBlue);
        var borderPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
        var margin = ScreenMetrics.LayoutMargin;

        var sliderLeft = LabelColumnWidth + margin;
        var sliderRight = bounds.Width - ValueColumnWidth - margin;
        var sliderWidth = sliderRight - sliderLeft;

        for (var i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            var y = i * RowHeight;
            var trackY = y + (RowHeight - 8) / 2;
            var trackH = 8.0;

            // Label
            var label = new FormattedText(p.Label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(label, new Point(margin, y + (RowHeight - label.Height) / 2));

            // Slider track
            context.DrawRectangle(trackBrush, borderPen,
                new Rect(sliderLeft, trackY, sliderWidth, trackH),
                trackH / 2, trackH / 2);

            // Slider fill
            var range = p.Max - p.Min;
            if (range > 0)
            {
                var fraction = Math.Clamp((_editValues[i] - p.Min) / range, 0, 1);
                var fillW = sliderWidth * fraction;
                if (fillW > 0)
                {
                    context.DrawRectangle(fillBrush, null,
                        new Rect(sliderLeft, trackY, fillW, trackH),
                        trackH / 2, trackH / 2);
                }

                // Thumb
                var thumbX = sliderLeft + sliderWidth * fraction;
                context.DrawEllipse(new SolidColorBrush(QgcColors.ButtonHighlight), null,
                    new Point(thumbX, trackY + trackH / 2), 7, 7);
            }

            // Value text
            var valueStr = _editValues[i].ToString("F3", CultureInfo.InvariantCulture);
            var valueText = new FormattedText(valueStr, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, boldTypeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(valueText, new Point(sliderRight + margin, y + (RowHeight - valueText.Height) / 2));
        }
    }

    private int _draggingIndex = -1;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var parameters = Parameters;
        if (parameters.Count == 0) return;

        var row = (int)(pos.Y / RowHeight);
        if (row < 0 || row >= parameters.Count) return;

        var margin = ScreenMetrics.LayoutMargin;
        var sliderLeft = LabelColumnWidth + margin;
        var sliderRight = Bounds.Width - ValueColumnWidth - margin;

        if (pos.X >= sliderLeft && pos.X <= sliderRight)
        {
            _draggingIndex = row;
            e.Pointer.Capture(this);
            UpdateSliderValue(pos);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_draggingIndex >= 0)
        {
            UpdateSliderValue(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_draggingIndex >= 0 && _editValues != null)
        {
            var p = Parameters[_draggingIndex];
            ParameterChanged?.Invoke(this, (p.Name, _editValues[_draggingIndex]));
            _draggingIndex = -1;
            e.Pointer.Capture(null);
        }
    }

    private void UpdateSliderValue(Point pos)
    {
        if (_draggingIndex < 0 || _editValues == null) return;
        var parameters = Parameters;
        if (_draggingIndex >= parameters.Count) return;

        var p = parameters[_draggingIndex];
        var margin = ScreenMetrics.LayoutMargin;
        var sliderLeft = LabelColumnWidth + margin;
        var sliderRight = Bounds.Width - ValueColumnWidth - margin;
        var sliderWidth = sliderRight - sliderLeft;

        if (sliderWidth <= 0) return;
        var fraction = Math.Clamp((pos.X - sliderLeft) / sliderWidth, 0, 1);
        var raw = p.Min + fraction * (p.Max - p.Min);

        // Snap to step
        if (p.Step > 0)
            raw = Math.Round(raw / p.Step) * p.Step;

        _editValues[_draggingIndex] = Math.Clamp(raw, p.Min, p.Max);
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 500 : availableSize.Width;
        var h = Parameters.Count * RowHeight;
        return new Size(w, h);
    }
}

// ---------------------------------------------------------------------------
// 6. JoystickConfigPanel
// QGC equivalent: Joystick/JoystickConfig*.qml
// Axis mapping section + button mapping section.
// ---------------------------------------------------------------------------

/// <summary>
/// Joystick configuration panel with axis mapping rows (axis index selector,
/// reversed checkbox, deadband slider) and button mapping list.
/// </summary>
public sealed class JoystickConfigPanel : Control
{
    private const double AxisRowHeight = 36;
    private const double ButtonRowHeight = 28;
    private const double SectionHeaderHeight = 32;
    private const double SectionSpacing = 16;
    private static readonly string[] AxisFunctions = ["Roll", "Pitch", "Yaw", "Throttle"];

    public static readonly StyledProperty<IReadOnlyList<AxisMapping>> AxisMappingsProperty =
        AvaloniaProperty.Register<JoystickConfigPanel, IReadOnlyList<AxisMapping>>(
            nameof(AxisMappings), Array.Empty<AxisMapping>());

    public static readonly StyledProperty<IReadOnlyList<ButtonMapping>> ButtonMappingsProperty =
        AvaloniaProperty.Register<JoystickConfigPanel, IReadOnlyList<ButtonMapping>>(
            nameof(ButtonMappings), Array.Empty<ButtonMapping>());

    static JoystickConfigPanel()
    {
        AffectsRender<JoystickConfigPanel>(AxisMappingsProperty, ButtonMappingsProperty);
        AffectsMeasure<JoystickConfigPanel>(AxisMappingsProperty, ButtonMappingsProperty);
    }

    public IReadOnlyList<AxisMapping> AxisMappings
    {
        get => GetValue(AxisMappingsProperty);
        set => SetValue(AxisMappingsProperty, value);
    }

    public IReadOnlyList<ButtonMapping> ButtonMappings
    {
        get => GetValue(ButtonMappingsProperty);
        set => SetValue(ButtonMappingsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 100 || bounds.Height < 20) return;

        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var boldTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var headerTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var margin = ScreenMetrics.LayoutMargin;
        var w = bounds.Width;

        // Column layout for axis rows
        var functionColW = 90.0;
        var axisColW = 60.0;
        var reversedColW = 80.0;
        var y = 0.0;

        // ---- Axis Mapping Section Header ----
        var axisHeader = new FormattedText("Axis Mappings", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, headerTypeface, ScreenMetrics.MediumFontPointSize,
            new SolidColorBrush(QgcColors.Text));
        context.DrawText(axisHeader, new Point(margin, y + (SectionHeaderHeight - axisHeader.Height) / 2));

        // Header underline
        var headerPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
        y += SectionHeaderHeight;
        context.DrawLine(headerPen, new Point(margin, y), new Point(w - margin, y));
        y += 2;

        // Column headers
        var colHeaderBrush = new SolidColorBrush(QgcColors.TextSecondary);
        var chFunction = new FormattedText("Function", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize, colHeaderBrush);
        var chAxis = new FormattedText("Axis", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize, colHeaderBrush);
        var chReversed = new FormattedText("Reversed", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize, colHeaderBrush);
        var chDeadband = new FormattedText("Deadband", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize, colHeaderBrush);

        var colX = margin;
        context.DrawText(chFunction, new Point(colX, y + 2)); colX += functionColW;
        context.DrawText(chAxis, new Point(colX, y + 2)); colX += axisColW;
        context.DrawText(chReversed, new Point(colX, y + 2)); colX += reversedColW;
        context.DrawText(chDeadband, new Point(colX, y + 2));
        y += 20;

        // ---- Axis rows ----
        var axisMappings = AxisMappings;
        var rowBgAlt = new SolidColorBrush(QgcColors.WindowShadeDark);
        var rowBg = new SolidColorBrush(QgcColors.WindowShade);
        var checkBrush = new SolidColorBrush(QgcColors.ColorGreen);
        var uncheckBrush = new SolidColorBrush(QgcColors.WindowShadeDark);
        var checkBorderPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
        var trackBrush = new SolidColorBrush(QgcColors.WindowShadeDark);
        var dbFillBrush = new SolidColorBrush(QgcColors.ColorBlue);

        for (var i = 0; i < axisMappings.Count; i++)
        {
            var am = axisMappings[i];
            var bg = i % 2 == 0 ? rowBg : rowBgAlt;
            context.DrawRectangle(bg, null, new Rect(0, y, w, AxisRowHeight));

            colX = margin;

            // Function name
            var funcText = new FormattedText(am.Function, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(funcText, new Point(colX, y + (AxisRowHeight - funcText.Height) / 2));
            colX += functionColW;

            // Axis index
            var axisText = new FormattedText($"Axis {am.AxisIndex}", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(axisText, new Point(colX, y + (AxisRowHeight - axisText.Height) / 2));
            colX += axisColW;

            // Reversed checkbox
            var cbSize = ScreenMetrics.ImplicitCheckBoxHeight;
            var cbY = y + (AxisRowHeight - cbSize) / 2;
            var cbBrush = am.Reversed ? checkBrush : uncheckBrush;
            context.DrawRectangle(cbBrush, checkBorderPen,
                new Rect(colX, cbY, cbSize, cbSize),
                3, 3);
            if (am.Reversed)
            {
                // Checkmark lines
                var ckPen = new Pen(new SolidColorBrush(QgcColors.ButtonText), 2);
                context.DrawLine(ckPen,
                    new Point(colX + 3, cbY + cbSize / 2),
                    new Point(colX + cbSize / 2 - 1, cbY + cbSize - 4));
                context.DrawLine(ckPen,
                    new Point(colX + cbSize / 2 - 1, cbY + cbSize - 4),
                    new Point(colX + cbSize - 3, cbY + 3));
            }
            colX += reversedColW;

            // Deadband slider (compact)
            var dbTrackW = w - colX - margin - 40;
            if (dbTrackW > 20)
            {
                var dbTrackH = 6.0;
                var dbTrackY = y + (AxisRowHeight - dbTrackH) / 2;
                context.DrawRectangle(trackBrush, checkBorderPen,
                    new Rect(colX, dbTrackY, dbTrackW, dbTrackH),
                    dbTrackH / 2, dbTrackH / 2);

                var dbFrac = Math.Clamp(am.Deadband / 0.2, 0, 1);
                var dbFillW = dbTrackW * dbFrac;
                if (dbFillW > 0)
                {
                    context.DrawRectangle(dbFillBrush, null,
                        new Rect(colX, dbTrackY, dbFillW, dbTrackH),
                        dbTrackH / 2, dbTrackH / 2);
                }

                // Deadband value
                var dbValText = new FormattedText(am.Deadband.ToString("F2", CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, ScreenMetrics.SmallFontPointSize,
                    new SolidColorBrush(QgcColors.TextSecondary));
                context.DrawText(dbValText, new Point(colX + dbTrackW + 4,
                    y + (AxisRowHeight - dbValText.Height) / 2));
            }

            y += AxisRowHeight;
        }

        y += SectionSpacing;

        // ---- Button Mapping Section Header ----
        var btnHeader = new FormattedText("Button Mappings", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, headerTypeface, ScreenMetrics.MediumFontPointSize,
            new SolidColorBrush(QgcColors.Text));
        context.DrawText(btnHeader, new Point(margin, y + (SectionHeaderHeight - btnHeader.Height) / 2));
        y += SectionHeaderHeight;
        context.DrawLine(headerPen, new Point(margin, y), new Point(w - margin, y));
        y += 2;

        // Button column headers
        var bhButton = new FormattedText("Button", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize, colHeaderBrush);
        var bhAction = new FormattedText("Action", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize, colHeaderBrush);
        context.DrawText(bhButton, new Point(margin, y + 2));
        context.DrawText(bhAction, new Point(margin + 80, y + 2));
        y += 20;

        // ---- Button rows ----
        var buttonMappings = ButtonMappings;
        for (var i = 0; i < buttonMappings.Count; i++)
        {
            var bm = buttonMappings[i];
            var bg = i % 2 == 0 ? rowBg : rowBgAlt;
            context.DrawRectangle(bg, null, new Rect(0, y, w, ButtonRowHeight));

            var btnIdText = new FormattedText($"Button {bm.ButtonIndex}", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(btnIdText, new Point(margin, y + (ButtonRowHeight - btnIdText.Height) / 2));

            var actionText = new FormattedText(bm.ActionName, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(actionText, new Point(margin + 80, y + (ButtonRowHeight - actionText.Height) / 2));

            y += ButtonRowHeight;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;

        var axisCount = AxisMappings.Count;
        var buttonCount = ButtonMappings.Count;

        var h = SectionHeaderHeight + 2 + 20 + axisCount * AxisRowHeight +
                SectionSpacing +
                SectionHeaderHeight + 2 + 20 + buttonCount * ButtonRowHeight;
        return new Size(w, h);
    }
}

// ---------------------------------------------------------------------------
// 7. PowerConfigPanel
// QGC equivalent: AutoPilotPlugins/APMPowerComponent.qml
// Battery cell count, voltage fields, current sensor calibration.
// ---------------------------------------------------------------------------

/// <summary>
/// Power configuration panel with battery cell count selector, voltage per cell
/// fields (full/empty), and current sensor multiplier.
/// Renders labeled rows with editable field areas.
/// </summary>
public sealed class PowerConfigPanel : Control
{
    private const double RowHeight = 40;
    private const double LabelColumnWidth = 160;
    private const double FieldWidth = 100;
    private const double TopPadding = 10;

    public static readonly StyledProperty<int> CellCountProperty =
        AvaloniaProperty.Register<PowerConfigPanel, int>(nameof(CellCount), 4);

    public static readonly StyledProperty<double> FullVoltageProperty =
        AvaloniaProperty.Register<PowerConfigPanel, double>(nameof(FullVoltage), 4.2);

    public static readonly StyledProperty<double> EmptyVoltageProperty =
        AvaloniaProperty.Register<PowerConfigPanel, double>(nameof(EmptyVoltage), 3.5);

    public static readonly StyledProperty<double> CurrentMultiplierProperty =
        AvaloniaProperty.Register<PowerConfigPanel, double>(nameof(CurrentMultiplier), 1.0);

    static PowerConfigPanel()
    {
        AffectsRender<PowerConfigPanel>(CellCountProperty, FullVoltageProperty,
            EmptyVoltageProperty, CurrentMultiplierProperty);
    }

    public int CellCount
    {
        get => GetValue(CellCountProperty);
        set => SetValue(CellCountProperty, Math.Clamp(value, 1, 14));
    }

    public double FullVoltage
    {
        get => GetValue(FullVoltageProperty);
        set => SetValue(FullVoltageProperty, value);
    }

    public double EmptyVoltage
    {
        get => GetValue(EmptyVoltageProperty);
        set => SetValue(EmptyVoltageProperty, value);
    }

    public double CurrentMultiplier
    {
        get => GetValue(CurrentMultiplierProperty);
        set => SetValue(CurrentMultiplierProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 100 || bounds.Height < 40) return;

        var w = bounds.Width;
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var boldTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
        var headerTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var margin = ScreenMetrics.LayoutMargin;
        var borderPen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);

        var y = TopPadding;

        // Section header
        var header = new FormattedText("Power Configuration", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, headerTypeface, ScreenMetrics.MediumFontPointSize,
            new SolidColorBrush(QgcColors.Text));
        context.DrawText(header, new Point(margin, y));
        y += header.Height + 8;
        context.DrawLine(borderPen, new Point(margin, y), new Point(w - margin, y));
        y += 6;

        // Parameter rows
        var rows = new (string Label, string Value, string Units)[]
        {
            ("Battery Cell Count", CellCount.ToString(CultureInfo.InvariantCulture), "cells"),
            ("Full Voltage (per cell)", FullVoltage.ToString("F2", CultureInfo.InvariantCulture), "V"),
            ("Empty Voltage (per cell)", EmptyVoltage.ToString("F2", CultureInfo.InvariantCulture), "V"),
            ("Current Multiplier", CurrentMultiplier.ToString("F2", CultureInfo.InvariantCulture), "A/V"),
        };

        var rowBg = new SolidColorBrush(QgcColors.WindowShade);
        var rowBgAlt = new SolidColorBrush(QgcColors.WindowShadeDark);
        var fieldBg = new SolidColorBrush(QgcColors.Window);

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var bg = i % 2 == 0 ? rowBg : rowBgAlt;
            context.DrawRectangle(bg, null, new Rect(0, y, w, RowHeight));

            // Label
            var labelText = new FormattedText(row.Label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(labelText, new Point(margin, y + (RowHeight - labelText.Height) / 2));

            // Field background
            var fieldX = LabelColumnWidth + margin;
            var fieldY = y + (RowHeight - 26) / 2;
            context.DrawRectangle(fieldBg, borderPen,
                new Rect(fieldX, fieldY, FieldWidth, 26),
                ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

            // Value in field
            var valueText = new FormattedText(row.Value, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, boldTypeface, ScreenMetrics.DefaultFontPointSize,
                new SolidColorBrush(QgcColors.Text));
            context.DrawText(valueText, new Point(
                fieldX + (FieldWidth - valueText.Width) / 2,
                fieldY + (26 - valueText.Height) / 2));

            // Units
            var unitsText = new FormattedText(row.Units, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(QgcColors.TextSecondary));
            context.DrawText(unitsText, new Point(
                fieldX + FieldWidth + ScreenMetrics.StandardMargin,
                y + (RowHeight - unitsText.Height) / 2));

            y += RowHeight;
        }

        // Calculated totals section
        y += 10;
        var totalFullV = CellCount * FullVoltage;
        var totalEmptyV = CellCount * EmptyVoltage;
        var totalText = new FormattedText(
            $"Pack Voltage: {totalFullV:F1}V (full) / {totalEmptyV:F1}V (empty)",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            boldTypeface, ScreenMetrics.DefaultFontPointSize,
            new SolidColorBrush(QgcColors.ColorBlue));
        context.DrawText(totalText, new Point(margin, y));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 500 : availableSize.Width;
        // Header + 4 rows + totals
        var h = TopPadding + 30 + 6 + 4 * RowHeight + 10 + 24;
        return new Size(w, h);
    }
}

// ────────────────────────────────────────────────────────────────
// 7. AutotuneControl
//    QGC equivalent: AutotuneUI.qml (AutopilotPlugins/Common)
//    Simple: Start button + live status label + horizontal progress bar.
//    Button is enabled only when CanTune=true and IsTuning=false.
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Autotune panel: a start button, live status label, and progress bar.
/// Mirrors QGC's AutotuneUI.qml — relies on the caller to set
/// <see cref="CanTune"/> (vehicle airborne), <see cref="IsTuning"/>,
/// <see cref="StatusText"/>, and <see cref="Progress"/> (0–1).
/// Clicking the button area fires <see cref="StartTuningRequested"/> when eligible.
/// </summary>
public sealed class AutotuneControl : Control
{
    private const double ButtonH   = 40;
    private const double StatusH   = 22;
    private const double ProgressH = 8;
    private const double BarRadius = 4;
    private const double Gap       = 8;

    public static readonly StyledProperty<bool> CanTuneProperty =
        AvaloniaProperty.Register<AutotuneControl, bool>(nameof(CanTune), false);

    public static readonly StyledProperty<bool> IsTuningProperty =
        AvaloniaProperty.Register<AutotuneControl, bool>(nameof(IsTuning), false);

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<AutotuneControl, string>(nameof(StatusText), "");

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AutotuneControl, double>(nameof(Progress), 0.0);

    static AutotuneControl()
    {
        AffectsRender<AutotuneControl>(CanTuneProperty, IsTuningProperty,
                                       StatusTextProperty, ProgressProperty);
        AffectsMeasure<AutotuneControl>(IsTuningProperty, StatusTextProperty);
    }

    public bool CanTune
    {
        get => GetValue(CanTuneProperty);
        set => SetValue(CanTuneProperty, value);
    }

    public bool IsTuning
    {
        get => GetValue(IsTuningProperty);
        set => SetValue(IsTuningProperty, value);
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public event EventHandler? StartTuningRequested;

    /// <summary>Arms the tuning sequence if the vehicle is eligible.</summary>
    public void RequestStartTuning()
    {
        if (CanTune && !IsTuning)
        {
            IsTuning  = true;
            Progress  = 0;
            StartTuningRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Resets state after tuning completes or is cancelled.</summary>
    public void ResetTuning()
    {
        IsTuning   = false;
        Progress   = 0;
        StatusText = "";
    }

    public override void Render(DrawingContext context)
    {
        var w          = Bounds.Width;
        var typeface   = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var boldTf     = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var canStart   = CanTune && !IsTuning;

        // ── Button ──
        var btnBg = new SolidColorBrush(canStart
            ? QgcColors.PrimaryButtonFill
            : QgcColors.WindowShade);
        context.DrawRectangle(btnBg, null,
            new Rect(0, 0, w, ButtonH),
            ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

        var btnCaption  = IsTuning ? "Autotune in progress…" : (CanTune ? "Start Autotune" : "Autotune (vehicle must be airborne)");
        var captionBrush = new SolidColorBrush(canStart ? QgcColors.ButtonText : QgcColors.DisabledText);
        var captionFt   = new FormattedText(btnCaption, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, boldTf,
            ScreenMetrics.DefaultFontPointSize, captionBrush);
        context.DrawText(captionFt,
            new Point((w - captionFt.Width) / 2, (ButtonH - captionFt.Height) / 2));

        var y = ButtonH + Gap;

        // ── Status text ──
        if (!string.IsNullOrEmpty(StatusText))
        {
            var statusBrush = new SolidColorBrush(IsTuning
                ? QgcColors.ColorBlue : QgcColors.TextSecondary);
            var statusFt = new FormattedText(StatusText, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface,
                ScreenMetrics.DefaultFontPointSize, statusBrush);
            context.DrawText(statusFt, new Point((w - statusFt.Width) / 2, y));
            y += StatusH;
        }

        // ── Progress bar ──
        if (IsTuning)
        {
            context.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
                new Rect(0, y, w, ProgressH), BarRadius, BarRadius);
            var fillW = Math.Clamp(Progress, 0.0, 1.0) * w;
            if (fillW > 0)
                context.DrawRectangle(new SolidColorBrush(QgcColors.ColorBlue), null,
                    new Rect(0, y, fillW, ProgressH), BarRadius, BarRadius);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        var h = ButtonH + Gap;
        if (!string.IsNullOrEmpty(StatusText)) h += StatusH + Gap;
        if (IsTuning)                          h += ProgressH + Gap;
        return new Size(w, h);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (new Rect(0, 0, Bounds.Width, ButtonH).Contains(e.GetPosition(this)))
            RequestStartTuning();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CustomCommandButton  (#153 — send a custom MAVLink command from the UI)
// Renders a button labeled CommandName.  When clicked it raises
// CommandRequested with the configured MAVLink command ID and parameters.
// Optional ConfirmationRequired delays the event until the user confirms.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class CustomCommandButton : Control
{
    public static readonly StyledProperty<string> CommandNameProperty =
        AvaloniaProperty.Register<CustomCommandButton, string>(nameof(CommandName), "Command");
    public static readonly StyledProperty<string> CommandDescriptionProperty =
        AvaloniaProperty.Register<CustomCommandButton, string>(nameof(CommandDescription), string.Empty);
    public static readonly StyledProperty<int>    MavCommandIdProperty =
        AvaloniaProperty.Register<CustomCommandButton, int>(nameof(MavCommandId), 0);
    public static readonly StyledProperty<float>  Param1Property =
        AvaloniaProperty.Register<CustomCommandButton, float>(nameof(Param1), 0f);
    public static readonly StyledProperty<float>  Param2Property =
        AvaloniaProperty.Register<CustomCommandButton, float>(nameof(Param2), 0f);
    public static readonly StyledProperty<float>  Param3Property =
        AvaloniaProperty.Register<CustomCommandButton, float>(nameof(Param3), 0f);
    public static readonly StyledProperty<bool>   ConfirmationRequiredProperty =
        AvaloniaProperty.Register<CustomCommandButton, bool>(nameof(ConfirmationRequired), false);
    public static readonly StyledProperty<bool>   CommandEnabledProperty =
        AvaloniaProperty.Register<CustomCommandButton, bool>(nameof(CommandEnabled), true);

    static CustomCommandButton()
    {
        AffectsRender<CustomCommandButton>(CommandNameProperty, CommandEnabledProperty, ConfirmationRequiredProperty);
        FocusableProperty.OverrideMetadata<CustomCommandButton>(new StyledPropertyMetadata<bool>(true));
    }

    public string CommandName        { get => GetValue(CommandNameProperty);        set => SetValue(CommandNameProperty, value); }
    public string CommandDescription { get => GetValue(CommandDescriptionProperty); set => SetValue(CommandDescriptionProperty, value); }
    public int    MavCommandId       { get => GetValue(MavCommandIdProperty);       set => SetValue(MavCommandIdProperty, value); }
    public float  Param1             { get => GetValue(Param1Property);             set => SetValue(Param1Property, value); }
    public float  Param2             { get => GetValue(Param2Property);             set => SetValue(Param2Property, value); }
    public float  Param3             { get => GetValue(Param3Property);             set => SetValue(Param3Property, value); }
    public bool   ConfirmationRequired { get => GetValue(ConfirmationRequiredProperty); set => SetValue(ConfirmationRequiredProperty, value); }
    public bool   CommandEnabled     { get => GetValue(CommandEnabledProperty);     set => SetValue(CommandEnabledProperty, value); }

    /// <summary>Raised when the button is clicked (and confirmation is not required or has been accepted).</summary>
    public event EventHandler<(int CommandId, float P1, float P2, float P3)>? CommandRequested;
    /// <summary>Raised when ConfirmationRequired=true to ask the host to show a confirm dialog.</summary>
    public event EventHandler<string>? ConfirmationAsked;

    private bool _hovered;
    private bool _pendingConfirm;

    public override void Render(DrawingContext ctx)
    {
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);
        bool enabled = CommandEnabled;

        // Background
        Color bgColor = _pendingConfirm ? QgcColors.ColorOrange :
                        !enabled        ? QgcColors.WindowShade  :
                        _hovered        ? QgcColors.PrimaryButtonFill : QgcColors.ButtonFill;
        ctx.DrawRectangle(new SolidColorBrush(bgColor), null, bounds, ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

        // Label
        var labelText = _pendingConfirm ? $"Confirm: {CommandName}" : CommandName;
        var textColor = !enabled ? QgcColors.DisabledText : QgcColors.ButtonText;
        var ft = new FormattedText(labelText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9, new SolidColorBrush(textColor));
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
        if (!CommandEnabled) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (ConfirmationRequired && !_pendingConfirm)
        {
            _pendingConfirm = true;
            InvalidateVisual();
            ConfirmationAsked?.Invoke(this, CommandName);
        }
        else
        {
            _pendingConfirm = false;
            CommandRequested?.Invoke(this, (MavCommandId, Param1, Param2, Param3));
            InvalidateVisual();
        }
        e.Handled = true;
    }

    /// <summary>Call from host when the user dismisses the confirmation dialog without accepting.</summary>
    public void CancelConfirmation()
    {
        _pendingConfirm = false;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfw = ScreenMetrics.DefaultFontPixelWidth;
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var ft  = new FormattedText(CommandName, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9, new SolidColorBrush(QgcColors.ButtonText));
        double w = ft.Width + dfw * 2;
        return new Size(Math.Max(w, ScreenMetrics.ImplicitButtonWidth), ScreenMetrics.ImplicitButtonHeight);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// VehicleSummaryPanel  (#172 — compact vehicle info panel at top of Setup view)
// Shows vehicle type, firmware version, and autopilot type in a summary row.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class VehicleSummaryPanel : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<string> VehicleTypeProperty =
        AvaloniaProperty.Register<VehicleSummaryPanel, string>(nameof(VehicleType), "Unknown");
    public static readonly StyledProperty<string> FirmwareVersionProperty =
        AvaloniaProperty.Register<VehicleSummaryPanel, string>(nameof(FirmwareVersion), "—");
    public static readonly StyledProperty<string> AutopilotTypeProperty =
        AvaloniaProperty.Register<VehicleSummaryPanel, string>(nameof(AutopilotType), "—");
    public static readonly StyledProperty<string> HardwareVersionProperty =
        AvaloniaProperty.Register<VehicleSummaryPanel, string>(nameof(HardwareVersion), string.Empty);
    public static readonly StyledProperty<bool>   IsVehicleConnectedProperty =
        AvaloniaProperty.Register<VehicleSummaryPanel, bool>(nameof(IsVehicleConnected), false);

    public string VehicleType         { get => GetValue(VehicleTypeProperty);         set => SetValue(VehicleTypeProperty, value); }
    public string FirmwareVersion     { get => GetValue(FirmwareVersionProperty);     set => SetValue(FirmwareVersionProperty, value); }
    public string AutopilotType       { get => GetValue(AutopilotTypeProperty);       set => SetValue(AutopilotTypeProperty, value); }
    public string HardwareVersion     { get => GetValue(HardwareVersionProperty);     set => SetValue(HardwareVersionProperty, value); }
    public bool   IsVehicleConnected  { get => GetValue(IsVehicleConnectedProperty);  set => SetValue(IsVehicleConnectedProperty, value); }
}

// ── #183 FirmwareUpgradePanel ─────────────────────────────────────────────────
public class FirmwareUpgradePanel : Control
{
    public static readonly StyledProperty<double> FUPProgressProperty =
        AvaloniaProperty.Register<FirmwareUpgradePanel, double>("FUPProgress", 0.0);
    public static readonly StyledProperty<string> FUPStatusTextProperty =
        AvaloniaProperty.Register<FirmwareUpgradePanel, string>("FUPStatusText", string.Empty);
    public static readonly StyledProperty<string> FUPFirmwareVersionProperty =
        AvaloniaProperty.Register<FirmwareUpgradePanel, string>("FUPFirmwareVersion", string.Empty);
    public static readonly StyledProperty<bool>   FUPIsUpgradingProperty =
        AvaloniaProperty.Register<FirmwareUpgradePanel, bool>("FUPIsUpgrading", false);
    public static readonly StyledProperty<bool>   FUPSucceededProperty =
        AvaloniaProperty.Register<FirmwareUpgradePanel, bool>("FUPSucceeded", false);
    public static readonly StyledProperty<bool>   FUPFailedProperty =
        AvaloniaProperty.Register<FirmwareUpgradePanel, bool>("FUPFailed", false);

    public double FUPProgress        { get => GetValue(FUPProgressProperty);        set => SetValue(FUPProgressProperty, value); }
    public string FUPStatusText      { get => GetValue(FUPStatusTextProperty);      set => SetValue(FUPStatusTextProperty, value); }
    public string FUPFirmwareVersion { get => GetValue(FUPFirmwareVersionProperty); set => SetValue(FUPFirmwareVersionProperty, value); }
    public bool   FUPIsUpgrading     { get => GetValue(FUPIsUpgradingProperty);     set => SetValue(FUPIsUpgradingProperty, value); }
    public bool   FUPSucceeded       { get => GetValue(FUPSucceededProperty);       set => SetValue(FUPSucceededProperty, value); }
    public bool   FUPFailed          { get => GetValue(FUPFailedProperty);          set => SetValue(FUPFailedProperty, value); }

    public event EventHandler? UpgradeRequested;
    public event EventHandler? CancelRequested;

    static FirmwareUpgradePanel()
    {
        AffectsRender<FirmwareUpgradePanel>(FUPProgressProperty, FUPStatusTextProperty,
            FUPFirmwareVersionProperty, FUPIsUpgradingProperty, FUPSucceededProperty, FUPFailedProperty);
    }

    private Rect _actionRect;

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        // Panel background
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder)),
            new Rect(0, 0, w, h), br);

        double y = dfh * 0.4;

        // Title
        string title = FUPSucceeded ? "Firmware Update Complete" :
                       FUPFailed    ? "Firmware Update Failed"   :
                       FUPIsUpgrading? "Updating Firmware…"      : "Firmware Upgrade";
        Color titleColor = FUPSucceeded ? QgcColors.ColorGreen :
                           FUPFailed    ? QgcColors.ColorRed    : QgcColors.Text;
        var titleFt = new FormattedText(title, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 1.0, new SolidColorBrush(titleColor));
        dc.DrawText(titleFt, new Point((w - titleFt.Width) / 2, y));
        y += dfh * 1.4;

        // Version line
        if (!string.IsNullOrEmpty(FUPFirmwareVersion))
        {
            var verFt = new FormattedText($"Version: {FUPFirmwareVersion}",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(verFt, new Point((w - verFt.Width) / 2, y));
            y += dfh * 1.2;
        }

        // Progress bar (only when upgrading)
        if (FUPIsUpgrading)
        {
            double barH   = dfh * 0.9;
            double barW   = w - 32;
            double barX   = 16;
            dc.DrawRectangle(new SolidColorBrush(QgcColors.Window), null,
                new Rect(barX, y, barW, barH), barH / 2);
            double fillW = barW * Math.Clamp(FUPProgress, 0, 1);
            if (fillW > 0)
                dc.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
                    new Rect(barX, y, fillW, barH), barH / 2);

            // Percentage
            string pctStr = $"{FUPProgress * 100:F0}%";
            var pctFt = new FormattedText(pctStr, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.ButtonText));
            dc.DrawText(pctFt, new Point(barX + (barW - pctFt.Width) / 2, y + (barH - pctFt.Height) / 2));
            y += barH + dfh * 0.4;
        }

        // Status text
        if (!string.IsNullOrEmpty(FUPStatusText))
        {
            var statusFt = new FormattedText(FUPStatusText, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(statusFt, new Point((w - statusFt.Width) / 2, y));
            y += dfh * 1.2;
        }

        // Action button
        if (!FUPIsUpgrading || FUPFailed || FUPSucceeded)
        {
            string btnLabel = FUPIsUpgrading ? "Cancel" : (FUPSucceeded || FUPFailed ? "Close" : "Upgrade");
            double btnH = ScreenMetrics.ImplicitButtonHeight;
            double btnW = ScreenMetrics.DefaultFontPixelWidth * 10;
            double btnX = (w - btnW) / 2;
            double btnY = h - btnH - dfh * 0.5;
            _actionRect = new Rect(btnX, btnY, btnW, btnH);
            var btnColor = FUPIsUpgrading ? QgcColors.ColorRed : QgcColors.PrimaryButtonFill;
            dc.DrawRectangle(new SolidColorBrush(btnColor), null, _actionRect, br);
            var btnFt = new FormattedText(btnLabel, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(QgcColors.ButtonText));
            dc.DrawText(btnFt, new Point(btnX + (btnW - btnFt.Width) / 2, btnY + (btnH - btnFt.Height) / 2));
        }
        else
        {
            _actionRect = new Rect(-1, -1, 0, 0);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_actionRect.Width > 0 && _actionRect.Contains(e.GetPosition(this)))
        {
            if (FUPIsUpgrading)
                CancelRequested?.Invoke(this, EventArgs.Empty);
            else
                UpgradeRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : ScreenMetrics.DefaultFontPixelWidth * 30;
        return new Size(w, dfh * 9);
    }
}

// ── #191 ParameterGroupHeader ─────────────────────────────────────────────────
public class ParameterGroupHeader : Control
{
    public static readonly StyledProperty<string> PGHGroupNameProperty =
        AvaloniaProperty.Register<ParameterGroupHeader, string>("PGHGroupName", string.Empty);
    public static readonly StyledProperty<int>    PGHParamCountProperty =
        AvaloniaProperty.Register<ParameterGroupHeader, int>("PGHParamCount", 0);
    public static readonly StyledProperty<bool>   PGHIsExpandedProperty =
        AvaloniaProperty.Register<ParameterGroupHeader, bool>("PGHIsExpanded", true);

    public string PGHGroupName  { get => GetValue(PGHGroupNameProperty);  set => SetValue(PGHGroupNameProperty, value); }
    public int    PGHParamCount { get => GetValue(PGHParamCountProperty); set => SetValue(PGHParamCountProperty, value); }
    public bool   PGHIsExpanded { get => GetValue(PGHIsExpandedProperty); set => SetValue(PGHIsExpandedProperty, value); }

    public event EventHandler? ToggleRequested;

    static ParameterGroupHeader()
    {
        AffectsRender<ParameterGroupHeader>(PGHGroupNameProperty, PGHParamCountProperty, PGHIsExpandedProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        // Background
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder)), new Rect(0, 0, w, h), br);

        // Chevron
        bool expanded  = PGHIsExpanded;
        double chevX   = 10;
        double chevY   = h / 2;
        double chevSz  = dfh * 0.35;
        var chevPen    = new Pen(new SolidColorBrush(QgcColors.Text), 1.8);
        if (expanded)
        {
            dc.DrawLine(chevPen, new Point(chevX, chevY - chevSz), new Point(chevX + chevSz, chevY + chevSz));
            dc.DrawLine(chevPen, new Point(chevX + chevSz, chevY + chevSz), new Point(chevX + chevSz * 2, chevY - chevSz));
        }
        else
        {
            dc.DrawLine(chevPen, new Point(chevX, chevY - chevSz), new Point(chevX + chevSz, chevY + chevSz));
            dc.DrawLine(chevPen, new Point(chevX, chevY + chevSz), new Point(chevX + chevSz, chevY - chevSz));
        }

        // Group name
        var nameFt = new FormattedText(PGHGroupName, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 0.9, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(nameFt, new Point(chevX + chevSz * 2 + 8, (h - nameFt.Height) / 2));

        // Count badge
        if (PGHParamCount > 0)
        {
            string cntStr   = PGHParamCount.ToString();
            var cntFt = new FormattedText(cntStr, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(QgcColors.ButtonText));
            double badgeW = cntFt.Width + 10;
            double badgeH = dfh * 1.0;
            double badgeX = w - badgeW - 8;
            double badgeY = (h - badgeH) / 2;
            dc.DrawRectangle(new SolidColorBrush(QgcColors.Button), null,
                new Rect(badgeX, badgeY, badgeW, badgeH), badgeH / 2);
            dc.DrawText(cntFt, new Point(badgeX + (badgeW - cntFt.Width) / 2, badgeY + (badgeH - cntFt.Height) / 2));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ToggleRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 280;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 2.0);
    }
}

// ── #195 JoystickAxisDisplay ──────────────────────────────────────────────────
public class JoystickAxisDisplay : Control
{
    public static readonly StyledProperty<int>    JAAxisIndexProperty =
        AvaloniaProperty.Register<JoystickAxisDisplay, int>("JAAxisIndex", 0);
    public static readonly StyledProperty<double> JAAxisValueProperty =
        AvaloniaProperty.Register<JoystickAxisDisplay, double>("JAAxisValue", 0.0);
    public static readonly StyledProperty<double> JATrimValueProperty =
        AvaloniaProperty.Register<JoystickAxisDisplay, double>("JATrimValue", 0.0);
    public static readonly StyledProperty<double> JADeadbandProperty =
        AvaloniaProperty.Register<JoystickAxisDisplay, double>("JADeadband", 0.0);
    public static readonly StyledProperty<string> JAAxisNameProperty =
        AvaloniaProperty.Register<JoystickAxisDisplay, string>("JAAxisName", string.Empty);
    public static readonly StyledProperty<bool>   JAIsReversedProperty =
        AvaloniaProperty.Register<JoystickAxisDisplay, bool>("JAIsReversed", false);

    public int    JAAxisIndex  { get => GetValue(JAAxisIndexProperty);  set => SetValue(JAAxisIndexProperty, value); }
    public double JAAxisValue  { get => GetValue(JAAxisValueProperty);  set => SetValue(JAAxisValueProperty, value); }
    public double JATrimValue  { get => GetValue(JATrimValueProperty);  set => SetValue(JATrimValueProperty, value); }
    public double JADeadband   { get => GetValue(JADeadbandProperty);   set => SetValue(JADeadbandProperty, value); }
    public string JAAxisName   { get => GetValue(JAAxisNameProperty);   set => SetValue(JAAxisNameProperty, value); }
    public bool   JAIsReversed { get => GetValue(JAIsReversedProperty); set => SetValue(JAIsReversedProperty, value); }

    static JoystickAxisDisplay()
    {
        AffectsRender<JoystickAxisDisplay>(JAAxisIndexProperty, JAAxisValueProperty, JATrimValueProperty,
            JADeadbandProperty, JAAxisNameProperty, JAIsReversedProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // Axis label
        string axLabel = string.IsNullOrEmpty(JAAxisName) ? $"Axis {JAAxisIndex}" : JAAxisName;
        var lblFt = new FormattedText(axLabel, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(lblFt, new Point(0, (h - lblFt.Height) / 2));

        double barX = lblFt.Width + 8;
        double barW = w - barX - dfh * 3;
        double barH = h * 0.3;
        double barY = (h - barH) / 2;

        if (barW < 20) return;

        // Track
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
            new Rect(barX, barY, barW, barH), barH / 2);

        double centerX = barX + barW / 2;

        // Deadband zone
        double db = Math.Clamp(JADeadband, 0, 1);
        if (db > 0)
        {
            double dbW = barW * db / 2;
            dc.FillRectangle(new SolidColorBrush(Color.FromArgb(40, 255, 165, 0)),
                new Rect(centerX - dbW, barY, dbW * 2, barH));
        }

        // Fill from center based on axis value
        double val = Math.Clamp(JAAxisValue, -1, 1);
        if (JAIsReversed) val = -val;
        double fillW = Math.Abs(val) * barW / 2;
        double fillX = val >= 0 ? centerX : centerX - fillW;
        Color fillColor = Math.Abs(val) > 0.9 ? QgcColors.ColorOrange : QgcColors.ColorGreen;
        if (fillW > 0)
            dc.FillRectangle(new SolidColorBrush(fillColor), new Rect(fillX, barY + 1, fillW, barH - 2));

        // Center tick
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.ColorGrey), 1),
            new Point(centerX, barY - 2), new Point(centerX, barY + barH + 2));

        // Trim marker (triangle above bar)
        double trimX = centerX + JATrimValue * barW / 2;
        double tSize = 4;
        var trimGeo = new StreamGeometry();
        using (var ctx = trimGeo.Open())
        {
            ctx.BeginFigure(new Point(trimX, barY - 1), true);
            ctx.LineTo(new Point(trimX - tSize, barY - 1 - tSize * 1.5));
            ctx.LineTo(new Point(trimX + tSize, barY - 1 - tSize * 1.5));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(new SolidColorBrush(QgcColors.ColorBlue), null, trimGeo);

        // Value text
        var valFt = new FormattedText($"{JAAxisValue:F2}", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(valFt, new Point(barX + barW + 4, (h - valFt.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 280;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 1.8);
    }
}

// ── #198 MotorTestButton ──────────────────────────────────────────────────────
public class MotorTestButton : Control
{
    public static readonly StyledProperty<int>    MTMotorIndexProperty =
        AvaloniaProperty.Register<MotorTestButton, int>("MTMotorIndex", 0);
    public static readonly StyledProperty<double> MTThrottleProperty =
        AvaloniaProperty.Register<MotorTestButton, double>("MTThrottle", 0.0);
    public static readonly StyledProperty<bool>   MTIsRunningProperty =
        AvaloniaProperty.Register<MotorTestButton, bool>("MTIsRunning", false);

    public int    MTMotorIndex { get => GetValue(MTMotorIndexProperty); set => SetValue(MTMotorIndexProperty, value); }
    public double MTThrottle   { get => GetValue(MTThrottleProperty);   set => SetValue(MTThrottleProperty, value); }
    public bool   MTIsRunning  { get => GetValue(MTIsRunningProperty);  set => SetValue(MTIsRunningProperty, value); }

    public event EventHandler<double>? ThrottleChangeRequested;
    public event EventHandler?         TestToggleRequested;

    static MotorTestButton()
    {
        AffectsRender<MotorTestButton>(MTMotorIndexProperty, MTThrottleProperty, MTIsRunningProperty);
    }

    private Rect _toggleRect;
    private Rect _sliderRect;

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        bool   running = MTIsRunning;
        double throttle = Math.Clamp(MTThrottle, 0, 1);

        // Motor label
        var mLabel = new FormattedText($"M{MTMotorIndex + 1}", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.Bold),
            dfh * 1.0, new SolidColorBrush(running ? QgcColors.ColorGreen : QgcColors.Text));
        dc.DrawText(mLabel, new Point(4, (h - mLabel.Height) / 2));

        // ON/OFF toggle button
        double btnW = dfh * 3.0;
        double btnH = h * 0.65;
        double btnX = w - btnW - 4;
        double btnY = (h - btnH) / 2;
        _toggleRect = new Rect(btnX, btnY, btnW, btnH);
        dc.DrawRectangle(
            new SolidColorBrush(running ? QgcColors.ColorRed : QgcColors.PrimaryButtonFill),
            null, _toggleRect, br);
        var btnFt = new FormattedText(running ? "STOP" : "RUN",
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.8,
            new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(btnFt, new Point(btnX + (btnW - btnFt.Width) / 2, btnY + (btnH - btnFt.Height) / 2));

        // Throttle slider track
        double sliderX = mLabel.Width + 8;
        double sliderW = btnX - sliderX - 8;
        double sliderH = h * 0.3;
        double sliderY = (h - sliderH) / 2;
        if (sliderW > 20)
        {
            _sliderRect = new Rect(sliderX, sliderY, sliderW, sliderH);
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, _sliderRect, sliderH / 2);
            double fillW = sliderW * throttle;
            if (fillW > 0)
                dc.DrawRectangle(
                    new SolidColorBrush(running ? QgcColors.ColorOrange : QgcColors.ColorGreen),
                    null, new Rect(sliderX, sliderY, fillW, sliderH), sliderH / 2);
            // Pct label
            var pctFt = new FormattedText($"{throttle * 100:F0}%",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(QgcColors.Text));
            dc.DrawText(pctFt, new Point(sliderX + (sliderW - pctFt.Width) / 2, (h - pctFt.Height) / 2));
        }
        else
        {
            _sliderRect = new Rect(-1, -1, 0, 0);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        if (_toggleRect.Contains(pos)) { TestToggleRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        else if (_sliderRect.Width > 0 && _sliderRect.Contains(pos))
        {
            double ratio = (pos.X - _sliderRect.X) / _sliderRect.Width;
            ThrottleChangeRequested?.Invoke(this, Math.Clamp(ratio, 0, 1));
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 220;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 2.0);
    }
}

// ── #199 SensorCalibrationStatus ─────────────────────────────────────────────
public class SensorCalibrationStatus : Control
{
    public static readonly StyledProperty<int>    SCSStepCountProperty =
        AvaloniaProperty.Register<SensorCalibrationStatus, int>("SCSStepCount", 6);
    public static readonly StyledProperty<int>    SCSCurrentStepProperty =
        AvaloniaProperty.Register<SensorCalibrationStatus, int>("SCSCurrentStep", -1);
    public static readonly StyledProperty<string> SCSStepLabelProperty =
        AvaloniaProperty.Register<SensorCalibrationStatus, string>("SCSStepLabel", string.Empty);
    public static readonly StyledProperty<bool>   SCSIsCompleteProperty =
        AvaloniaProperty.Register<SensorCalibrationStatus, bool>("SCSIsComplete", false);

    public int    SCSStepCount   { get => GetValue(SCSStepCountProperty);   set => SetValue(SCSStepCountProperty, value); }
    public int    SCSCurrentStep { get => GetValue(SCSCurrentStepProperty); set => SetValue(SCSCurrentStepProperty, value); }
    public string SCSStepLabel   { get => GetValue(SCSStepLabelProperty);   set => SetValue(SCSStepLabelProperty, value); }
    public bool   SCSIsComplete  { get => GetValue(SCSIsCompleteProperty);  set => SetValue(SCSIsCompleteProperty, value); }

    static SensorCalibrationStatus()
    {
        AffectsRender<SensorCalibrationStatus>(SCSStepCountProperty, SCSCurrentStepProperty,
            SCSStepLabelProperty, SCSIsCompleteProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        int    steps    = Math.Max(1, SCSStepCount);
        int    current  = SCSCurrentStep;
        bool   complete = SCSIsComplete;

        // Step dots row
        double dotR   = Math.Min(h * 0.3, 8);
        double dotSpan= dotR * 2 + 4;
        double totalW = steps * dotSpan - 4;
        double dotsX  = (w - totalW) / 2;
        double dotsY  = h * 0.25;

        for (int i = 0; i < steps; i++)
        {
            bool done    = complete || i < current;
            bool active  = i == current && !complete;
            double cx    = dotsX + i * dotSpan + dotR;
            Color  color = done   ? QgcColors.ColorGreen
                         : active ? QgcColors.PrimaryButtonFill
                                  : QgcColors.ColorGrey;
            dc.DrawEllipse(new SolidColorBrush(color),
                active ? new Pen(new SolidColorBrush(Colors.White), 1.5) : null,
                new Point(cx, dotsY + dotR), dotR, dotR);

            // Connector line between dots
            if (i < steps - 1)
            {
                double lineY = dotsY + dotR;
                Color  lc    = i < current ? QgcColors.ColorGreen : QgcColors.ColorGrey;
                dc.DrawLine(new Pen(new SolidColorBrush(lc), 1),
                    new Point(cx + dotR, lineY), new Point(cx + dotSpan - dotR, lineY));
            }
        }

        // Step label
        string label = complete ? "Calibration complete" :
                       current < 0 ? "Waiting to start…" : SCSStepLabel;
        Color labelColor = complete ? QgcColors.ColorGreen : QgcColors.Text;
        var labelFt = new FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(labelColor));
        dc.DrawText(labelFt, new Point((w - labelFt.Width) / 2, dotsY + dotR * 2 + 6));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 260;
        return new Size(w, dfh * 3.2);
    }
}

// ── #210 CalibrationAreaDisplay ───────────────────────────────────────────────
public class CalibrationAreaDisplay : Control
{
    // Face indices: 0=+X(Nose), 1=-X(Tail), 2=+Y(Right), 3=-Y(Left), 4=+Z(Down), 5=-Z(Up)
    public static readonly StyledProperty<int> CADActiveFaceProperty =
        AvaloniaProperty.Register<CalibrationAreaDisplay, int>("CADActiveFace", -1);
    public static readonly StyledProperty<int> CADCompletedFacesProperty =
        AvaloniaProperty.Register<CalibrationAreaDisplay, int>("CADCompletedFaces", 0);

    public int CADActiveFace     { get => GetValue(CADActiveFaceProperty);     set => SetValue(CADActiveFaceProperty, value); }
    public int CADCompletedFaces { get => GetValue(CADCompletedFacesProperty); set => SetValue(CADCompletedFacesProperty, value); }

    static CalibrationAreaDisplay()
    {
        AffectsRender<CalibrationAreaDisplay>(CADActiveFaceProperty, CADCompletedFacesProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // 6 faces laid out in cross: top=Up, mid-row=Left/Nose/Right/Tail, bottom=Down
        double cellSz = Math.Min(w / 4, h / 3) * 0.85;
        double cx     = w / 2;
        double cy     = h / 2;

        // Positions: (col, row) offsets in cell units from center
        (int face, double col, double row, string label)[] layout =
        {
            (5, 0, -1, "Up"),    // -Z
            (3, -1, 0, "Left"),  // -Y
            (0, 0,  0, "Nose"),  // +X
            (2, 1,  0, "Right"), // +Y
            (1, 2,  0, "Tail"),  // -X
            (4, 0,  1, "Down"),  // +Z
        };

        double br = ScreenMetrics.DefaultBorderRadius;

        foreach (var (face, col, row, label) in layout)
        {
            bool active    = CADActiveFace == face;
            bool completed = (CADCompletedFaces & (1 << face)) != 0;

            double rx = cx + col * (cellSz + 4) - cellSz / 2;
            double ry = cy + row * (cellSz + 4) - cellSz / 2;

            Color bg = active    ? QgcColors.PrimaryButtonFill
                     : completed ? QgcColors.ColorGreen
                                 : QgcColors.WindowShade;
            dc.DrawRectangle(new SolidColorBrush(bg),
                new Pen(new SolidColorBrush(active ? QgcColors.Text : QgcColors.GroupBorder)),
                new Rect(rx, ry, cellSz, cellSz), br);

            var lFt = new FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(active || completed ? QgcColors.ButtonText : QgcColors.TextSecondary));
            dc.DrawText(lFt, new Point(rx + (cellSz - lFt.Width) / 2, ry + (cellSz - lFt.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double sz = !double.IsInfinity(availableSize.Width) ? availableSize.Width : dfh * 20;
        return new Size(sz, sz * 0.75);
    }
}

// ── #211 FlightModeConfigRow ──────────────────────────────────────────────────
public class FlightModeConfigRow : Control
{
    public static readonly StyledProperty<string> FMCModeNameProperty =
        AvaloniaProperty.Register<FlightModeConfigRow, string>("FMCModeName", string.Empty);
    public static readonly StyledProperty<int>    FMCChannelProperty =
        AvaloniaProperty.Register<FlightModeConfigRow, int>("FMCChannel", 5);
    public static readonly StyledProperty<int>    FMCSwitchPositionProperty =
        AvaloniaProperty.Register<FlightModeConfigRow, int>("FMCSwitchPosition", 0);
    public static readonly StyledProperty<bool>   FMCIsCurrentProperty =
        AvaloniaProperty.Register<FlightModeConfigRow, bool>("FMCIsCurrent", false);

    public string FMCModeName        { get => GetValue(FMCModeNameProperty);        set => SetValue(FMCModeNameProperty, value); }
    public int    FMCChannel         { get => GetValue(FMCChannelProperty);         set => SetValue(FMCChannelProperty, value); }
    public int    FMCSwitchPosition  { get => GetValue(FMCSwitchPositionProperty);  set => SetValue(FMCSwitchPositionProperty, value); }
    public bool   FMCIsCurrent       { get => GetValue(FMCIsCurrentProperty);       set => SetValue(FMCIsCurrentProperty, value); }

    static FlightModeConfigRow()
    {
        AffectsRender<FlightModeConfigRow>(FMCModeNameProperty, FMCChannelProperty,
            FMCSwitchPositionProperty, FMCIsCurrentProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // Current mode highlight
        if (FMCIsCurrent)
            dc.FillRectangle(new SolidColorBrush(Color.FromArgb(25, 0, 180, 80)), new Rect(0, 0, w, h));
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Active dot
        double dotR = h * 0.22;
        Color  dotC = FMCIsCurrent ? QgcColors.ColorGreen : QgcColors.ColorGrey;
        dc.DrawEllipse(new SolidColorBrush(dotC), null, new Point(dotR + 4, h / 2), dotR, dotR);

        // Mode name
        var nameFt = new FormattedText(FMCModeName, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(nameFt, new Point(dotR * 2 + 10, (h - nameFt.Height) / 2));

        // Channel + switch position (right side)
        // Draw 6 small position bars, highlight active
        int maxPos = 6;
        double barW2 = 10;
        double barH2 = 6;
        double posStartX = w - maxPos * (barW2 + 2) - 8;
        double posY      = (h - barH2) / 2;
        for (int i = 0; i < maxPos; i++)
        {
            bool active = i == FMCSwitchPosition;
            dc.FillRectangle(
                new SolidColorBrush(active ? QgcColors.PrimaryButtonFill : QgcColors.WindowShade),
                new Rect(posStartX + i * (barW2 + 2), posY, barW2, barH2));
        }

        // Channel label
        var chFt = new FormattedText($"CH{FMCChannel}", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(chFt, new Point(posStartX - chFt.Width - 6, (h - chFt.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 280;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 1.8);
    }
}

// ── #221 RadioChannelRow ──────────────────────────────────────────────────────
// Single RC channel row: channel name (left), PWM bar (centre), numeric value (right).
// RCRChannelName, RCRPwmValue (1000–2000), RCRMinPwm, RCRMaxPwm, RCRTrimPwm.
// Bar fill is centred at trim, green within range, red outside.
public sealed class RadioChannelRow : Control
{
    public static readonly StyledProperty<string> RCRChannelNameProperty =
        AvaloniaProperty.Register<RadioChannelRow, string>("RCRChannelName", "CH1");
    public static readonly StyledProperty<int>    RCRPwmValueProperty =
        AvaloniaProperty.Register<RadioChannelRow, int>("RCRPwmValue", 1500);
    public static readonly StyledProperty<int>    RCRMinPwmProperty =
        AvaloniaProperty.Register<RadioChannelRow, int>("RCRMinPwm", 1000);
    public static readonly StyledProperty<int>    RCRMaxPwmProperty =
        AvaloniaProperty.Register<RadioChannelRow, int>("RCRMaxPwm", 2000);
    public static readonly StyledProperty<int>    RCRTrimPwmProperty =
        AvaloniaProperty.Register<RadioChannelRow, int>("RCRTrimPwm", 1500);

    static RadioChannelRow()
    {
        AffectsRender<RadioChannelRow>(RCRChannelNameProperty, RCRPwmValueProperty,
            RCRMinPwmProperty, RCRMaxPwmProperty, RCRTrimPwmProperty);
    }

    public string RCRChannelName { get => GetValue(RCRChannelNameProperty); set => SetValue(RCRChannelNameProperty, value); }
    public int    RCRPwmValue    { get => GetValue(RCRPwmValueProperty);    set => SetValue(RCRPwmValueProperty, value); }
    public int    RCRMinPwm      { get => GetValue(RCRMinPwmProperty);      set => SetValue(RCRMinPwmProperty, value); }
    public int    RCRMaxPwm      { get => GetValue(RCRMaxPwmProperty);      set => SetValue(RCRMaxPwmProperty, value); }
    public int    RCRTrimPwm     { get => GetValue(RCRTrimPwmProperty);     set => SetValue(RCRTrimPwmProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Channel name (left ~20%)
        double nameW = w * 0.20;
        var nameFt = new FormattedText(RCRChannelName,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.78, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(nameFt, new Point(dfw * 0.4, (h - nameFt.Height) / 2));

        // Value (right ~18%)
        double valW = w * 0.18;
        var valFt = new FormattedText(RCRPwmValue.ToString(),
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.Text));
        dc.DrawText(valFt, new Point(w - valW + (valW - valFt.Width) / 2, (h - valFt.Height) / 2));

        // PWM bar (centre)
        double barX = nameW + dfw;
        double barW = w - nameW - valW - dfw * 2;
        double barH = h * 0.35;
        double barY = (h - barH) / 2;
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
            new Rect(barX, barY, barW, barH), 2, 2);

        int range = Math.Max(RCRMaxPwm - RCRMinPwm, 1);
        int trimN = Math.Clamp(RCRTrimPwm - RCRMinPwm, 0, range);
        int valN  = Math.Clamp(RCRPwmValue - RCRMinPwm, 0, range);
        double trimX  = barX + barW * trimN / range;
        double valPx  = barW * valN / range;
        double fillX  = Math.Min(trimX, barX + valPx);
        double fillW2 = Math.Abs(barX + valPx - trimX);
        bool inRange  = RCRPwmValue >= RCRMinPwm && RCRPwmValue <= RCRMaxPwm;
        if (fillW2 > 0)
            dc.DrawRectangle(new SolidColorBrush(inRange ? QgcColors.ColorGreen : QgcColors.ColorRed),
                null, new Rect(fillX, barY, fillW2, barH), 2, 2);
        dc.DrawLine(new Pen(new SolidColorBrush(Colors.White), 1.5),
            new Point(trimX, barY - 1), new Point(trimX, barY + barH + 1));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width) ? available.Width : 300;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}

// ── #232 BatteryConfigPanel ───────────────────────────────────────────────────
// Battery configuration form: cell count, capacity (mAh), chemistry type,
// and voltage thresholds.  Renders as a labelled-grid layout.
// BCPCellCount, BCPCapacityMah, BCPChemistry ("LiPo"/"LiIon"/"NiMH"),
// BCPLowVoltage, BCPCriticalVoltage (per cell).
public sealed class BatteryConfigPanel : Control
{
    public static readonly StyledProperty<int>    BCPCellCountProperty =
        AvaloniaProperty.Register<BatteryConfigPanel, int>("BCPCellCount", 4);
    public static readonly StyledProperty<int>    BCPCapacityMahProperty =
        AvaloniaProperty.Register<BatteryConfigPanel, int>("BCPCapacityMah", 5000);
    public static readonly StyledProperty<string> BCPChemistryProperty =
        AvaloniaProperty.Register<BatteryConfigPanel, string>("BCPChemistry", "LiPo");
    public static readonly StyledProperty<double> BCPLowVoltageProperty =
        AvaloniaProperty.Register<BatteryConfigPanel, double>("BCPLowVoltage", 3.5);
    public static readonly StyledProperty<double> BCPCriticalVoltageProperty =
        AvaloniaProperty.Register<BatteryConfigPanel, double>("BCPCriticalVoltage", 3.2);

    static BatteryConfigPanel()
    {
        AffectsRender<BatteryConfigPanel>(BCPCellCountProperty, BCPCapacityMahProperty,
            BCPChemistryProperty, BCPLowVoltageProperty, BCPCriticalVoltageProperty);
        AffectsMeasure<BatteryConfigPanel>(BCPCellCountProperty);
    }

    public int    BCPCellCount       { get => GetValue(BCPCellCountProperty);       set => SetValue(BCPCellCountProperty, value); }
    public int    BCPCapacityMah     { get => GetValue(BCPCapacityMahProperty);     set => SetValue(BCPCapacityMahProperty, value); }
    public string BCPChemistry       { get => GetValue(BCPChemistryProperty);       set => SetValue(BCPChemistryProperty, value); }
    public double BCPLowVoltage      { get => GetValue(BCPLowVoltageProperty);      set => SetValue(BCPLowVoltageProperty, value); }
    public double BCPCriticalVoltage { get => GetValue(BCPCriticalVoltageProperty); set => SetValue(BCPCriticalVoltageProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w    = Bounds.Width;
        double dfh  = ScreenMetrics.DefaultFontPixelHeight;
        double dfw  = ScreenMetrics.DefaultFontPixelWidth;
        double rowH = dfh * 2.0;
        double br   = ScreenMetrics.DefaultBorderRadius;
        double labelW = w * 0.42;
        double valX   = labelW + dfw;
        double valW   = w - labelW - dfw * 2;

        var rows = new (string Label, string Value)[]
        {
            ("Cell Count",           BCPCellCount.ToString()),
            ("Capacity (mAh)",       BCPCapacityMah.ToString()),
            ("Chemistry",            BCPChemistry),
            ("Low Voltage/cell (V)", $"{BCPLowVoltage:F2}"),
            ("Critical Voltage/cell","" + $"{BCPCriticalVoltage:F2}"),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            double rowY = i * rowH;
            // Alternating row background
            if (i % 2 == 1)
                dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
                    new Rect(0, rowY, w, rowH));

            var lbFt = new FormattedText(rows[i].Label,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.8, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(lbFt, new Point(dfw * 0.4, rowY + (rowH - lbFt.Height) / 2));

            // Value box
            var valRect = new Rect(valX, rowY + 3, valW, rowH - 6);
            dc.DrawRectangle(new SolidColorBrush(QgcColors.Window),
                new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5), valRect, br, br);

            var vlFt = new FormattedText(rows[i].Value,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.85, new SolidColorBrush(QgcColors.Text));
            dc.DrawText(vlFt, new Point(valX + dfw * 0.4, rowY + (rowH - vlFt.Height) / 2));
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width) ? available.Width : 300;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 2.0 * 5);
    }
}

// ── #239 ParameterSearchBar ───────────────────────────────────────────────────
// Search bar for the parameter editor: text input area + clear (×) button + match count.
// PSBSearchText drives the display; clicking Clear fires ClearRequested.
// PSBMatchCount shows "N matches" when > 0, or "No matches" in red when 0 and text is set.
public sealed class ParameterSearchBar : Control
{
    public static readonly StyledProperty<string> PSBSearchTextProperty =
        AvaloniaProperty.Register<ParameterSearchBar, string>("PSBSearchText", string.Empty);
    public static readonly StyledProperty<int>    PSBMatchCountProperty =
        AvaloniaProperty.Register<ParameterSearchBar, int>("PSBMatchCount", 0);
    public static readonly StyledProperty<string> PSBPlaceholderProperty =
        AvaloniaProperty.Register<ParameterSearchBar, string>("PSBPlaceholder", "Search parameters…");

    static ParameterSearchBar()
    {
        AffectsRender<ParameterSearchBar>(PSBSearchTextProperty, PSBMatchCountProperty, PSBPlaceholderProperty);
    }

    public string PSBSearchText  { get => GetValue(PSBSearchTextProperty);  set => SetValue(PSBSearchTextProperty, value); }
    public int    PSBMatchCount  { get => GetValue(PSBMatchCountProperty);  set => SetValue(PSBMatchCountProperty, value); }
    public string PSBPlaceholder { get => GetValue(PSBPlaceholderProperty); set => SetValue(PSBPlaceholderProperty, value); }

    public event EventHandler? ClearRequested;

    private Rect _clearRect;

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;
        double pad = 4;

        // Border box
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Window),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1),
            new Rect(0, 0, w, h), br, br);

        bool hasText = !string.IsNullOrEmpty(PSBSearchText);

        // Search text or placeholder
        string display = hasText ? PSBSearchText : PSBPlaceholder;
        Color  textC   = hasText ? QgcColors.Text : QgcColors.TextSecondary;
        double clearW  = hasText ? dfh * 1.4 : 0;

        var ft = new FormattedText(display,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.85, new SolidColorBrush(textC))
        { MaxTextWidth = w - pad * 2 - clearW };
        dc.DrawText(ft, new Point(pad, (h - ft.Height) / 2));

        // Clear button (×)
        if (hasText)
        {
            _clearRect = new Rect(w - clearW - pad, (h - clearW) / 2, clearW, clearW);
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, _clearRect,
                _clearRect.Height / 2, _clearRect.Height / 2);
            var xFt = new FormattedText("×",
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.85, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(xFt, new Point(_clearRect.X + (_clearRect.Width - xFt.Width) / 2,
                                       _clearRect.Y + (_clearRect.Height - xFt.Height) / 2));
        }
        else
        {
            _clearRect = new Rect(-1, -1, 0, 0);
        }

        // Match count (bottom-right outside box)
        if (hasText)
        {
            bool noMatch = PSBMatchCount == 0;
            string matchTxt = noMatch ? "No matches" : $"{PSBMatchCount} matches";
            Color  matchC   = noMatch ? QgcColors.ColorRed : QgcColors.TextSecondary;
            var mFt = new FormattedText(matchTxt,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.68, new SolidColorBrush(matchC));
            dc.DrawText(mFt, new Point(w - mFt.Width, h + 1));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_clearRect.Width > 0 && _clearRect.Contains(e.GetPosition(this)))
        {
            ClearRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width) ? available.Width : 280;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}

// ── #240 GimbalControlPanel ───────────────────────────────────────────────────
// Gimbal manual control panel: pitch slider (vertical), yaw slider (horizontal),
// and a lock/unlock toggle button.
// GCPPitch/GCPYaw are current positions (−90–+90 / −180–+180),
// GCPIsLocked toggles lock mode.  Raises PitchChanged/YawChanged/LockToggled.
public sealed class GimbalControlPanel : Control
{
    public static readonly StyledProperty<double> GCPPitchProperty =
        AvaloniaProperty.Register<GimbalControlPanel, double>("GCPPitch", 0.0);
    public static readonly StyledProperty<double> GCPYawProperty =
        AvaloniaProperty.Register<GimbalControlPanel, double>("GCPYaw", 0.0);
    public static readonly StyledProperty<bool>   GCPIsLockedProperty =
        AvaloniaProperty.Register<GimbalControlPanel, bool>("GCPIsLocked", false);

    static GimbalControlPanel()
    {
        AffectsRender<GimbalControlPanel>(GCPPitchProperty, GCPYawProperty, GCPIsLockedProperty);
    }

    public double GCPPitch    { get => GetValue(GCPPitchProperty);    set => SetValue(GCPPitchProperty, value); }
    public double GCPYaw      { get => GetValue(GCPYawProperty);      set => SetValue(GCPYawProperty, value); }
    public bool   GCPIsLocked { get => GetValue(GCPIsLockedProperty); set => SetValue(GCPIsLockedProperty, value); }

    public event EventHandler<double>? PitchChanged;
    public event EventHandler<double>? YawChanged;
    public event EventHandler?         LockToggled;

    private Rect _lockBtnRect;
    private Rect _pitchTrack;
    private Rect _yawTrack;

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;
        double br  = ScreenMetrics.DefaultBorderRadius;
        double pad = 8;

        // Yaw slider (top row, horizontal)
        double yawY   = pad;
        double yawH   = 10;
        double yawX   = pad + dfh * 1.5;
        double yawW   = w - yawX - pad;
        _yawTrack = new Rect(yawX, yawY + (dfh - yawH) / 2, yawW, yawH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, _yawTrack, br, br);
        double yawFrac = (GCPYaw + 180.0) / 360.0;
        double yawFillW = yawFrac * yawW;
        dc.DrawRectangle(new SolidColorBrush(QgcColors.ColorBlue), null,
            new Rect(_yawTrack.X, _yawTrack.Y, yawFillW, yawH), br, br);
        // Yaw label
        var yawLb = new FormattedText($"Y{GCPYaw:+0.0;-0.0;0}°",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.72, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(yawLb, new Point(pad, yawY + (dfh - yawLb.Height) / 2));

        // Pitch slider (left column, vertical)
        double pitchX  = pad;
        double pitchY  = yawY + dfh + pad;
        double pitchW  = 10;
        double pitchH  = h - pitchY - pad - dfh * 1.6;
        _pitchTrack = new Rect(pitchX + (dfh * 1.5 - pitchW) / 2, pitchY, pitchW, pitchH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, _pitchTrack, br, br);
        double pitchFrac = (GCPPitch + 90.0) / 180.0;
        double pitchFillH = pitchFrac * pitchH;
        dc.DrawRectangle(new SolidColorBrush(QgcColors.ColorGreen), null,
            new Rect(_pitchTrack.X, _pitchTrack.Y + pitchH - pitchFillH, pitchW, pitchFillH), br, br);
        var pitchLb = new FormattedText($"P{GCPPitch:+0.0;-0.0;0}°",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.72, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(pitchLb, new Point(pad, h - pad - pitchLb.Height - dfh * 0.8));

        // Lock button (bottom)
        double btnH  = dfh * 1.4;
        double btnW  = w - dfw * 2;
        double btnY  = h - btnH - pad;
        _lockBtnRect = new Rect(dfw, btnY, btnW, btnH);
        Color btnC   = GCPIsLocked ? QgcColors.ColorOrange : QgcColors.ButtonFill;
        dc.DrawRectangle(new SolidColorBrush(btnC), null, _lockBtnRect, br, br);
        var btnFt = new FormattedText(GCPIsLocked ? "Locked" : "Free",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.ButtonText));
        dc.DrawText(btnFt, new Point(_lockBtnRect.X + (_lockBtnRect.Width - btnFt.Width) / 2,
                                     btnY + (btnH - btnFt.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        if (_lockBtnRect.Width > 0 && _lockBtnRect.Contains(pos))
        {
            GCPIsLocked = !GCPIsLocked;
            LockToggled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (_yawTrack.Width > 0 && _yawTrack.Contains(pos))
        {
            double frac = Math.Clamp((pos.X - _yawTrack.X) / _yawTrack.Width, 0, 1);
            GCPYaw = frac * 360.0 - 180.0;
            YawChanged?.Invoke(this, GCPYaw);
            e.Handled = true;
        }
        else if (_pitchTrack.Height > 0 && _pitchTrack.Contains(pos))
        {
            double frac = Math.Clamp((pos.Y - _pitchTrack.Y) / _pitchTrack.Height, 0, 1);
            GCPPitch = (1 - frac) * 180.0 - 90.0;
            PitchChanged?.Invoke(this, GCPPitch);
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width)  ? available.Width  : 160;
        double h = !double.IsInfinity(available.Height) ? available.Height : 200;
        return new Size(w, h);
    }
}
