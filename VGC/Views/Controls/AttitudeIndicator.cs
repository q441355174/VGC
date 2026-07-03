using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace VGC.Views.Controls;

public class AttitudeIndicator : Control
{
    public static readonly StyledProperty<double> PitchProperty =
        AvaloniaProperty.Register<AttitudeIndicator, double>(nameof(Pitch));

    public static readonly StyledProperty<double> RollProperty =
        AvaloniaProperty.Register<AttitudeIndicator, double>(nameof(Roll));

    static AttitudeIndicator()
    {
        AffectsRender<AttitudeIndicator>(PitchProperty, RollProperty);
    }

    public double Pitch
    {
        get => GetValue(PitchProperty);
        set => SetValue(PitchProperty, value);
    }

    public double Roll
    {
        get => GetValue(RollProperty);
        set => SetValue(RollProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var size = Math.Min(bounds.Width, bounds.Height);
        if (size < 10) return;

        var cx = bounds.Width / 2;
        var cy = bounds.Height / 2;
        var radius = size / 2 - 2;

        // Clip to circle using RoundedRect with full radius
        using var _ = context.PushClip(new RoundedRect(new Rect(cx - radius, cy - radius, radius * 2, radius * 2), radius));

        // Apply roll rotation
        var rollRad = -Roll * Math.PI / 180;
        using var __ = context.PushTransform(
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateRotation(rollRad) *
            Matrix.CreateTranslation(cx, cy));

        // Pitch offset (pixels per degree)
        var pitchScale = radius / 30.0;
        var pitchOffset = Pitch * pitchScale;

        // Sky (blue)
        var skyBrush = new SolidColorBrush(Color.Parse("#2980b9"));
        context.DrawRectangle(skyBrush, null, new Rect(cx - radius * 2, cy - radius * 2 + pitchOffset, radius * 4, radius * 2));

        // Ground (brown)
        var groundBrush = new SolidColorBrush(Color.Parse("#8B6914"));
        context.DrawRectangle(groundBrush, null, new Rect(cx - radius * 2, cy + pitchOffset, radius * 4, radius * 2));

        // Horizon line
        var horizonPen = new Pen(Brushes.White, 2);
        context.DrawLine(horizonPen, new Point(cx - radius * 2, cy + pitchOffset), new Point(cx + radius * 2, cy + pitchOffset));

        // Pitch ladder
        var ladderPen = new Pen(Brushes.White, 1);
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        foreach (var deg in new[] { -20, -10, 10, 20 })
        {
            var y = cy + pitchOffset - deg * pitchScale;
            var halfWidth = radius * 0.25;
            context.DrawLine(ladderPen, new Point(cx - halfWidth, y), new Point(cx + halfWidth, y));

            var text = new FormattedText(
                Math.Abs(deg).ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface, 10, Brushes.White);
            context.DrawText(text, new Point(cx + halfWidth + 4, y - text.Height / 2));
        }

        // 5-degree marks
        var smallPen = new Pen(Brushes.White, 0.5);
        foreach (var deg in new[] { -25, -15, -5, 5, 15, 25 })
        {
            var y = cy + pitchOffset - deg * pitchScale;
            var halfWidth = radius * 0.12;
            context.DrawLine(smallPen, new Point(cx - halfWidth, y), new Point(cx + halfWidth, y));
        }

        // Pop roll transform for fixed elements
        // (Already inside the push scope, so the following draws with roll applied)

        // Aircraft reference (fixed, drawn last with no rotation)
        // We're inside the roll+clip scope, so these lines rotate with roll - which is correct for the ladder
        // The aircraft symbol should NOT rotate, but since we're inside the push, we need to counter-rotate
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var s = Math.Min(
            double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height);
        return new Size(s, s);
    }
}

public class CompassIndicator : Control
{
    public static readonly StyledProperty<double> HeadingProperty =
        AvaloniaProperty.Register<CompassIndicator, double>(nameof(Heading));

    static CompassIndicator()
    {
        AffectsRender<CompassIndicator>(HeadingProperty);
    }

    public double Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var size = Math.Min(bounds.Width, bounds.Height);
        if (size < 10) return;

        var cx = bounds.Width / 2;
        var cy = bounds.Height / 2;
        var radius = size / 2 - 4;

        // Background circle
        var bgBrush = new SolidColorBrush(Color.Parse("#0d1a24"));
        context.DrawEllipse(bgBrush, new Pen(new SolidColorBrush(Color.Parse("#2a3a48")), 2),
            new Point(cx, cy), radius, radius);

        // Rotate for heading
        using var _ = context.PushTransform(
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateRotation(-Heading * Math.PI / 180) *
            Matrix.CreateTranslation(cx, cy));

        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var tickPen = new Pen(Brushes.White, 1.5);
        var minorPen = new Pen(new SolidColorBrush(Color.Parse("#6b7d8e")), 1);

        // Cardinal and ordinal directions
        string[] labels = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        for (var i = 0; i < 360; i += 5)
        {
            var angle = i * Math.PI / 180;
            var innerRadius = i % 45 == 0 ? radius - 24 :
                              i % 15 == 0 ? radius - 16 :
                              i % 10 == 0 ? radius - 12 : radius - 8;
            var pen = i % 15 == 0 ? tickPen : minorPen;

            var x1 = cx + innerRadius * Math.Sin(angle);
            var y1 = cy - innerRadius * Math.Cos(angle);
            var x2 = cx + (radius - 2) * Math.Sin(angle);
            var y2 = cy - (radius - 2) * Math.Cos(angle);
            context.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
        }

        // Direction labels
        for (var i = 0; i < 8; i++)
        {
            var angle = i * 45 * Math.PI / 180;
            var labelRadius = radius - 34;
            var x = cx + labelRadius * Math.Sin(angle);
            var y = cy - labelRadius * Math.Cos(angle);

            var isCardinal = i % 2 == 0;
            var brush = isCardinal ? new SolidColorBrush(i == 0 ? Color.Parse("#e74c3c") : Colors.White)
                                   : new SolidColorBrush(Color.Parse("#91a4b5"));
            var text = new FormattedText(
                labels[i],
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface, isCardinal ? 14 : 10, brush);

            // Counter-rotate text so it stays readable
            using var __ = context.PushTransform(
                Matrix.CreateTranslation(-x, -y) *
                Matrix.CreateRotation(Heading * Math.PI / 180) *
                Matrix.CreateTranslation(x, y));
            context.DrawText(text, new Point(x - text.Width / 2, y - text.Height / 2));
        }

        // Pop heading rotation (scope ends)
        // Heading readout is drawn outside the rotation scope

        // Aircraft nose pointer (fixed - drawn after scope)
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var s = Math.Min(
            double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height);
        return new Size(s, s);
    }
}

public class VerticalGauge : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VerticalGauge, double>(nameof(Value));

    public static readonly StyledProperty<string> UnitsProperty =
        AvaloniaProperty.Register<VerticalGauge, string>(nameof(Units), "m");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<VerticalGauge, string>(nameof(Label), "ALT");

    public static readonly StyledProperty<double> RangeProperty =
        AvaloniaProperty.Register<VerticalGauge, double>(nameof(Range), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<VerticalGauge, double>(nameof(Step), 10);

    static VerticalGauge()
    {
        AffectsRender<VerticalGauge>(ValueProperty, UnitsProperty, LabelProperty, RangeProperty, StepProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Units
    {
        get => GetValue(UnitsProperty);
        set => SetValue(UnitsProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Range
    {
        get => GetValue(RangeProperty);
        set => SetValue(RangeProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 10 || bounds.Height < 10) return;

        var w = bounds.Width;
        var h = bounds.Height;

        // Background
        var bgBrush = new SolidColorBrush(Color.Parse("#D90d1a24"));
        context.DrawRectangle(bgBrush, new Pen(new SolidColorBrush(Color.Parse("#2a3a48")), 1),
            new Rect(0, 0, w, h), 4, 4);

        // Gauge area
        var gaugeTop = 24.0;
        var gaugeBottom = h - 24;
        var gaugeHeight = gaugeBottom - gaugeTop;
        var centerY = (gaugeTop + gaugeBottom) / 2;

        // Scale - center on current value
        var pixelsPerUnit = gaugeHeight / Range;
        var step = Step;
        var startValue = Value - Range / 2;
        var endValue = Value + Range / 2;

        var tickPen = new Pen(Brushes.White, 1);
        var minorPen = new Pen(new SolidColorBrush(Color.Parse("#4e6070")), 0.5);
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);

        // Draw tick marks
        var firstTick = Math.Ceiling(startValue / step) * step;
        for (var v = firstTick; v <= endValue; v += step)
        {
            var y = centerY - (v - Value) * pixelsPerUnit;
            if (y < gaugeTop || y > gaugeBottom) continue;

            var isMajor = Math.Abs(v % (step * 2)) < 0.001;
            var tickWidth = isMajor ? w * 0.3 : w * 0.15;
            var pen = isMajor ? tickPen : minorPen;

            context.DrawLine(pen, new Point(0, y), new Point(tickWidth, y));

            if (isMajor)
            {
                var text = new FormattedText(
                    v.ToString("F0"),
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface, 11, Brushes.White);
                context.DrawText(text, new Point(tickWidth + 4, y - text.Height / 2));
            }
        }

        // Current value box
        var boxHeight = 24.0;
        var boxBrush = new SolidColorBrush(Color.Parse("#1a3040"));
        var boxPen = new Pen(new SolidColorBrush(Color.Parse("#3498db")), 1.5);
        context.DrawRectangle(boxBrush, boxPen,
            new Rect(2, centerY - boxHeight / 2, w - 4, boxHeight), 3, 3);

        var valueText = new FormattedText(
            $"{Value:F1}",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold), 13,
            new SolidColorBrush(Color.Parse("#3498db")));
        context.DrawText(valueText, new Point((w - valueText.Width) / 2, centerY - valueText.Height / 2));

        // Label at top
        var labelText = new FormattedText(
            Label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface, 10, new SolidColorBrush(Color.Parse("#91a4b5")));
        context.DrawText(labelText, new Point((w - labelText.Width) / 2, 4));

        // Units at bottom
        var unitsText = new FormattedText(
            Units,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface, 10, new SolidColorBrush(Color.Parse("#6b7d8e")));
        context.DrawText(unitsText, new Point((w - unitsText.Width) / 2, h - 18));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 60 : Math.Min(availableSize.Width, 80);
        var h = double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height;
        return new Size(w, h);
    }
}

/// <summary>
/// Compass heading arrow — red chevron that rotates with vehicle heading.
/// Overlaid on top of CompassIndicator to show current bearing direction.
/// Equivalent to QGC FlightMap/Widgets/CompassHeadingIndicator.qml
/// Size ~1/3 of the parent compass diameter; Simplified=true draws all-red stroke.
/// </summary>
public sealed class CompassHeadingIndicator : Control
{
    public static readonly StyledProperty<double> HeadingProperty =
        AvaloniaProperty.Register<CompassHeadingIndicator, double>(nameof(Heading));

    public static readonly StyledProperty<bool> SimplifiedProperty =
        AvaloniaProperty.Register<CompassHeadingIndicator, bool>(nameof(Simplified));

    static CompassHeadingIndicator()
    {
        AffectsRender<CompassHeadingIndicator>(HeadingProperty, SimplifiedProperty);
    }

    public double Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public bool Simplified
    {
        get => GetValue(SimplifiedProperty);
        set => SetValue(SimplifiedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 4 || h < 4) return;

        var cx = w / 2;
        var cy = h / 2;

        // Rotate entire drawing by heading angle around center
        using var _ = context.PushTransform(
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateRotation(Heading * Math.PI / 180.0) *
            Matrix.CreateTranslation(cx, cy));

        // QGC draws two filled triangles sharing tip (cx,0) and notch (cx, h*0.75):
        //   Right half: bright red #EE3424   tip→(w,h)→notch
        //   Left half:  dark red  #C72B27    tip→(0,h)→notch
        var strokeColor = Simplified ? Color.Parse("#EE3424") : Colors.White;
        var pen = new Pen(new SolidColorBrush(strokeColor), 1);

        var rightGeo = new StreamGeometry();
        using (var sctx = rightGeo.Open())
        {
            sctx.BeginFigure(new Point(cx, 0), isFilled: true);
            sctx.LineTo(new Point(w,  h));
            sctx.LineTo(new Point(cx, h * 0.75));
            sctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(new SolidColorBrush(Color.Parse("#EE3424")), pen, rightGeo);

        var leftGeo = new StreamGeometry();
        using (var sctx = leftGeo.Open())
        {
            sctx.BeginFigure(new Point(cx, 0), isFilled: true);
            sctx.LineTo(new Point(0,  h));
            sctx.LineTo(new Point(cx, h * 0.75));
            sctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(new SolidColorBrush(Color.Parse("#C72B27")), pen, leftGeo);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var s = double.IsInfinity(availableSize.Width) ? 20 : Math.Min(availableSize.Width, 30);
        return new Size(s, s);
    }
}

// ────────────────────────────────────────────────────────────────
// 5. IntegratedAttitudeIndicator
//    QGC equivalent: FlightMap/Widgets/IntegratedAttitudeIndicator.qml
//    Circular roll-arc widget rendered around a compass circle:
//      • Gray track arc spanning ±30° from top
//      • Blue fill arc from 0° to the clamped roll angle
//      • White tick at the 0° (top) reference mark
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Circular roll-arc indicator painted around the perimeter of a compass circle.
/// Mirrors QGC's IntegratedAttitudeIndicator.qml — a thin annular arc
/// at the top of the widget that fills proportionally to roll angle.
/// <para>
/// Place this control with the same size as the parent compass control,
/// then set <see cref="CompassRadius"/> to the companion compass's radius.
/// </para>
/// </summary>
public sealed class IntegratedAttitudeIndicator : Control
{
    private const double MaxAngleDeg   = 30;
    private const double ArcThickRatio = 0.75; // × DefaultFontPixelHeight

    public static readonly StyledProperty<double> AttitudeAngleDegreesProperty =
        AvaloniaProperty.Register<IntegratedAttitudeIndicator, double>(
            nameof(AttitudeAngleDegrees), 0);

    public static readonly StyledProperty<double> CompassRadiusProperty =
        AvaloniaProperty.Register<IntegratedAttitudeIndicator, double>(
            nameof(CompassRadius), 48);

    static IntegratedAttitudeIndicator()
    {
        AffectsRender<IntegratedAttitudeIndicator>(
            AttitudeAngleDegreesProperty, CompassRadiusProperty);
        AffectsMeasure<IntegratedAttitudeIndicator>(CompassRadiusProperty);
    }

    /// <summary>Roll angle in degrees (+right / −left). Clamped internally to ±30°.</summary>
    public double AttitudeAngleDegrees
    {
        get => GetValue(AttitudeAngleDegreesProperty);
        set => SetValue(AttitudeAngleDegreesProperty, value);
    }

    /// <summary>Radius of the companion compass circle, in device pixels.</summary>
    public double CompassRadius
    {
        get => GetValue(CompassRadiusProperty);
        set => SetValue(CompassRadiusProperty, value);
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Returns a screen point at the given compass <paramref name="bearingDeg"/>
    /// (0 = North/up, increases CW) on a circle of radius <paramref name="r"/>
    /// centred at (<paramref name="cx"/>, <paramref name="cy"/>).
    /// </summary>
    private static Point BearingPt(double cx, double cy, double r, double bearingDeg)
    {
        var rad = bearingDeg * Math.PI / 180.0;
        return new Point(cx + r * Math.Sin(rad), cy - r * Math.Cos(rad));
    }

    /// <summary>Draws a circular arc as a stroked StreamGeometry.</summary>
    private static void DrawArc(DrawingContext ctx, double cx, double cy, double r,
        double bearingStart, double bearingEnd, IBrush strokeBrush, double thickness)
    {
        var span = bearingEnd - bearingStart;
        if (Math.Abs(span) < 0.01) return;

        var startPt  = BearingPt(cx, cy, r, bearingStart);
        var endPt    = BearingPt(cx, cy, r, bearingEnd);
        var isLarge  = Math.Abs(span) > 180;
        var sweep    = SweepDirection.Clockwise; // bearings always increase CW on screen

        var geo = new StreamGeometry();
        using (var gctx = geo.Open())
        {
            gctx.BeginFigure(startPt, false);
            gctx.ArcTo(endPt, new Size(r, r), 0, isLarge, sweep);
            gctx.EndFigure(false);
        }
        ctx.DrawGeometry(null, new Pen(strokeBrush, thickness), geo);
    }

    // ── Render ───────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var cx = w / 2;
        var cy = h / 2;

        var attThickness = ScreenMetrics.DefaultFontPixelHeight * ArcThickRatio;
        var attSpacing   = ScreenMetrics.DefaultFontPixelHeight / 4.0;
        var attRadius    = CompassRadius + attSpacing + attThickness / 2;

        // 1. Gray background track arc (±30° from North)
        DrawArc(context, cx, cy, attRadius, -MaxAngleDeg, +MaxAngleDeg,
            new SolidColorBrush(QgcColors.WindowShade), attThickness);

        // 2. Blue fill arc (from 0 to clamped roll, always drawn CW from smaller to larger)
        var clamped = Math.Clamp(AttitudeAngleDegrees, -MaxAngleDeg, MaxAngleDeg);
        if (Math.Abs(clamped) > 0.5)
        {
            var bStart = Math.Min(0, clamped);
            var bEnd   = Math.Max(0, clamped);
            DrawArc(context, cx, cy, attRadius, bStart, bEnd,
                new SolidColorBrush(QgcColors.PrimaryButtonFill), attThickness);
        }

        // 3. White tick mark at 0° (North / top)
        var tickPen  = new Pen(new SolidColorBrush(QgcColors.Text), 2);
        var tickHalf = attThickness / 2 + 1;
        var topPt    = BearingPt(cx, cy, attRadius, 0);

        // Tick direction: radially outward from centre
        var radInner = BearingPt(cx, cy, attRadius - tickHalf, 0);
        var radOuter = BearingPt(cx, cy, attRadius + tickHalf, 0);
        context.DrawLine(tickPen, radInner, radOuter);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var attThickness = ScreenMetrics.DefaultFontPixelHeight * ArcThickRatio;
        var attSpacing   = ScreenMetrics.DefaultFontPixelHeight / 4.0;
        var totalR       = CompassRadius + attSpacing + attThickness;
        return new Size(totalR * 2, totalR * 2);
    }
}
