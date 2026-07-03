using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.ObjectModel;
using System.Globalization;

namespace VGC.Views.Controls;

// ────────────────────────────────────────────────────────────────
// Data models
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Single parameter diff entry used by <see cref="ParameterDiffDialog"/>.
/// </summary>
public sealed record ParamDiff(string Name, string OldValue, string NewValue);

/// <summary>
/// Log entry metadata used by <see cref="LogDownloadPanel"/>.
/// </summary>
public sealed record OnboardLogEntry(int Id, long Size, DateTime Date);

// ────────────────────────────────────────────────────────────────
// 1. ParameterEditorDialog
//    QGC equivalent: QmlControls/ParameterEditorDialog.qml
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Modal dialog for editing a single parameter.
/// Extends <see cref="PopupDialog"/> with name, description, current/new value,
/// validation against min/max/enum constraints, and OK/Cancel actions.
/// </summary>
public class ParameterEditorDialog : PopupDialog
{
    public static readonly StyledProperty<string> ParameterNameProperty =
        AvaloniaProperty.Register<ParameterEditorDialog, string>(nameof(ParameterName), "");

    public static readonly StyledProperty<string> CurrentValueProperty =
        AvaloniaProperty.Register<ParameterEditorDialog, string>(nameof(CurrentValue), "");

    public static readonly StyledProperty<string> NewValueProperty =
        AvaloniaProperty.Register<ParameterEditorDialog, string>(nameof(NewValue), "");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ParameterEditorDialog, string>(nameof(Description), "");

    public static readonly StyledProperty<string> UnitsProperty =
        AvaloniaProperty.Register<ParameterEditorDialog, string>(nameof(Units), "");

    public static readonly StyledProperty<double?> MinProperty =
        AvaloniaProperty.Register<ParameterEditorDialog, double?>(nameof(Min));

    public static readonly StyledProperty<double?> MaxProperty =
        AvaloniaProperty.Register<ParameterEditorDialog, double?>(nameof(Max));

    public string ParameterName { get => GetValue(ParameterNameProperty); set => SetValue(ParameterNameProperty, value); }
    public string CurrentValue { get => GetValue(CurrentValueProperty); set => SetValue(CurrentValueProperty, value); }
    public string NewValue { get => GetValue(NewValueProperty); set => SetValue(NewValueProperty, value); }
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }
    public double? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }
    public double? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>
    /// When non-empty the value is treated as an enum and free-text entry is replaced
    /// by a selection from this list.
    /// </summary>
    public ObservableCollection<string> EnumValues { get; } = [];

    public string ValidationError { get; private set; } = "";

    public ParameterEditorDialog()
    {
        DialogTitle = "Parameter Editor";
        ShowAcceptButton = true;
        ShowCancelButton = true;
    }

    /// <summary>
    /// Validates <see cref="NewValue"/> against min/max/enum constraints.
    /// Sets <see cref="ValidationError"/> and returns true when valid.
    /// </summary>
    public bool Validate()
    {
        // Enum mode — value must be one of the allowed strings
        if (EnumValues.Count > 0)
        {
            if (!EnumValues.Contains(NewValue))
            {
                ValidationError = "Value must be one of the allowed options";
                return false;
            }

            ValidationError = "";
            return true;
        }

        if (string.IsNullOrWhiteSpace(NewValue))
        {
            ValidationError = "Value required";
            return false;
        }

        if (!double.TryParse(NewValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
        {
            ValidationError = "Invalid number";
            return false;
        }

        if (Min.HasValue && num < Min.Value)
        {
            ValidationError = $"Below minimum ({Min.Value})";
            return false;
        }

        if (Max.HasValue && num > Max.Value)
        {
            ValidationError = $"Above maximum ({Max.Value})";
            return false;
        }

        ValidationError = "";
        return true;
    }

    /// <summary>
    /// Validates then accepts (closes) the dialog.
    /// </summary>
    public void ValidateAndAccept()
    {
        if (Validate())
            Accept();
    }
}

// ────────────────────────────────────────────────────────────────
// 2. ParameterDiffDialog
//    QGC equivalent: QmlControls/ParameterDiffDialog.qml
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Shows parameter changes in a three-column table (Name | Old | New)
/// with changed rows highlighted. Extends <see cref="PopupDialog"/>.
/// </summary>
public class ParameterDiffDialog : PopupDialog
{
    /// <summary>
    /// List of parameter diffs to display.
    /// </summary>
    public ObservableCollection<ParamDiff> Diffs { get; } = [];

    public ParameterDiffDialog()
    {
        DialogTitle = "Parameter Changes";
        ShowAcceptButton = true;
        AcceptText = "Apply";
        ShowCancelButton = true;
    }

    /// <summary>
    /// Returns only the diffs where OldValue differs from NewValue.
    /// </summary>
    public IReadOnlyList<ParamDiff> ChangedDiffs =>
        Diffs.Where(d => d.OldValue != d.NewValue).ToArray();
}

// ────────────────────────────────────────────────────────────────
// 3. GeoTagPanel
//    QGC equivalent: AnalyzeView/GeoTagPage.qml
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Log-file + image-folder geo-tagging panel.
/// Vertical form with file/folder selectors, time-offset field,
/// status display, and a process button.
/// </summary>
public class GeoTagPanel : TemplatedControl
{
    public static readonly StyledProperty<string> LogFilePathProperty =
        AvaloniaProperty.Register<GeoTagPanel, string>(nameof(LogFilePath), "");

    public static readonly StyledProperty<string> ImageFolderPathProperty =
        AvaloniaProperty.Register<GeoTagPanel, string>(nameof(ImageFolderPath), "");

    public static readonly StyledProperty<double> TimeOffsetSecondsProperty =
        AvaloniaProperty.Register<GeoTagPanel, double>(nameof(TimeOffsetSeconds), 0);

    public static readonly StyledProperty<int> ProcessedCountProperty =
        AvaloniaProperty.Register<GeoTagPanel, int>(nameof(ProcessedCount), 0);

    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<GeoTagPanel, string>(nameof(Status), "Ready");

    public string LogFilePath { get => GetValue(LogFilePathProperty); set => SetValue(LogFilePathProperty, value); }
    public string ImageFolderPath { get => GetValue(ImageFolderPathProperty); set => SetValue(ImageFolderPathProperty, value); }
    public double TimeOffsetSeconds { get => GetValue(TimeOffsetSecondsProperty); set => SetValue(TimeOffsetSecondsProperty, value); }
    public int ProcessedCount { get => GetValue(ProcessedCountProperty); set => SetValue(ProcessedCountProperty, value); }
    public string Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public event EventHandler? ProcessRequested;
    public event EventHandler? SelectLogFileRequested;
    public event EventHandler? SelectImageFolderRequested;

    public bool CanProcess => !string.IsNullOrWhiteSpace(LogFilePath)
                           && !string.IsNullOrWhiteSpace(ImageFolderPath);

    public void RequestProcess()
    {
        if (CanProcess)
        {
            Status = "Processing...";
            ProcessRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RequestSelectLogFile() => SelectLogFileRequested?.Invoke(this, EventArgs.Empty);
    public void RequestSelectImageFolder() => SelectImageFolderRequested?.Invoke(this, EventArgs.Empty);
}

// ────────────────────────────────────────────────────────────────
// 4. VibrationDisplay
//    QGC equivalent: AnalyzeView/VibrationPage.qml
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Three-axis vibration bar display with threshold line and clip counts.
/// Custom control with <see cref="Render"/> override.
/// </summary>
public class VibrationDisplay : Control
{
    private const double ThresholdValue = 30.0; // m/s^2

    public static readonly StyledProperty<double> VibrationXProperty =
        AvaloniaProperty.Register<VibrationDisplay, double>(nameof(VibrationX));

    public static readonly StyledProperty<double> VibrationYProperty =
        AvaloniaProperty.Register<VibrationDisplay, double>(nameof(VibrationY));

    public static readonly StyledProperty<double> VibrationZProperty =
        AvaloniaProperty.Register<VibrationDisplay, double>(nameof(VibrationZ));

    public static readonly StyledProperty<int> ClipCount0Property =
        AvaloniaProperty.Register<VibrationDisplay, int>(nameof(ClipCount0));

    public static readonly StyledProperty<int> ClipCount1Property =
        AvaloniaProperty.Register<VibrationDisplay, int>(nameof(ClipCount1));

    public static readonly StyledProperty<int> ClipCount2Property =
        AvaloniaProperty.Register<VibrationDisplay, int>(nameof(ClipCount2));

    static VibrationDisplay()
    {
        AffectsRender<VibrationDisplay>(
            VibrationXProperty, VibrationYProperty, VibrationZProperty,
            ClipCount0Property, ClipCount1Property, ClipCount2Property);
    }

    public double VibrationX { get => GetValue(VibrationXProperty); set => SetValue(VibrationXProperty, value); }
    public double VibrationY { get => GetValue(VibrationYProperty); set => SetValue(VibrationYProperty, value); }
    public double VibrationZ { get => GetValue(VibrationZProperty); set => SetValue(VibrationZProperty, value); }
    public int ClipCount0 { get => GetValue(ClipCount0Property); set => SetValue(ClipCount0Property, value); }
    public int ClipCount1 { get => GetValue(ClipCount1Property); set => SetValue(ClipCount1Property, value); }
    public int ClipCount2 { get => GetValue(ClipCount2Property); set => SetValue(ClipCount2Property, value); }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 40 || bounds.Height < 60) return;

        var w = bounds.Width;
        var h = bounds.Height;
        var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
        var boldTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);

        // Background
        context.DrawRectangle(QgcColors.WindowShadeBrush, null, new Rect(0, 0, w, h),
            ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

        // Title
        var titleText = new FormattedText("Vibration", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, boldTypeface, ScreenMetrics.MediumFontPointSize, QgcColors.TextBrush);
        context.DrawText(titleText, new Point((w - titleText.Width) / 2, ScreenMetrics.StandardMargin));

        // Bar area
        var barAreaTop = titleText.Height + ScreenMetrics.StandardMargin * 3;
        var clipTextHeight = 20.0;
        var barAreaBottom = h - clipTextHeight - ScreenMetrics.StandardMargin * 2;
        var barAreaHeight = barAreaBottom - barAreaTop;
        if (barAreaHeight < 20) return;

        var barCount = 3;
        var barGap = ScreenMetrics.WidgetMargin;
        var totalGap = barGap * (barCount + 1);
        var barWidth = (w - totalGap) / barCount;

        // Scale: 0..60 m/s^2 (double the threshold for headroom)
        var maxScale = 60.0;

        var values = new[] { VibrationX, VibrationY, VibrationZ };
        var labels = new[] { "X", "Y", "Z" };
        var clips = new[] { ClipCount0, ClipCount1, ClipCount2 };
        var barColors = new[]
        {
            new SolidColorBrush(QgcColors.ColorRed),
            new SolidColorBrush(QgcColors.ColorGreen),
            new SolidColorBrush(QgcColors.ColorBlue)
        };

        for (var i = 0; i < barCount; i++)
        {
            var x = barGap + i * (barWidth + barGap);
            var clampedValue = Math.Clamp(values[i], 0, maxScale);
            var barH = barAreaHeight * (clampedValue / maxScale);
            var barY = barAreaBottom - barH;

            // Bar outline
            var outlinePen = new Pen(new SolidColorBrush(QgcColors.GroupBorder), 1);
            context.DrawRectangle(null, outlinePen, new Rect(x, barAreaTop, barWidth, barAreaHeight),
                ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

            // Filled bar
            var aboveThreshold = values[i] > ThresholdValue;
            IBrush fillBrush = aboveThreshold
                ? new SolidColorBrush(QgcColors.ColorRed)
                : barColors[i];
            context.DrawRectangle(fillBrush, null, new Rect(x, barY, barWidth, barH),
                ScreenMetrics.DefaultBorderRadius, ScreenMetrics.DefaultBorderRadius);

            // Axis label below bar
            var labelFmt = new FormattedText(labels[i], CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, boldTypeface, ScreenMetrics.DefaultFontPointSize, QgcColors.TextBrush);
            context.DrawText(labelFmt, new Point(x + (barWidth - labelFmt.Width) / 2, barAreaBottom + 2));

            // Value above bar
            var valueFmt = new FormattedText(values[i].ToString("F1"), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize,
                aboveThreshold ? QgcColors.ErrorBrush : QgcColors.TextBrush);
            context.DrawText(valueFmt, new Point(x + (barWidth - valueFmt.Width) / 2, barY - valueFmt.Height - 2));

            // Clip count text
            var clipFmt = new FormattedText($"Clip: {clips[i]}", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize,
                clips[i] > 0 ? QgcColors.WarningBrush : QgcColors.TextSecondaryBrush);
            context.DrawText(clipFmt, new Point(x + (barWidth - clipFmt.Width) / 2,
                barAreaBottom + labelFmt.Height + 4));
        }

        // Threshold line at 30 m/s^2
        var thresholdY = barAreaBottom - barAreaHeight * (ThresholdValue / maxScale);
        var thresholdPen = new Pen(QgcColors.WarningBrush, 1.5, DashStyle.Dash);
        context.DrawLine(thresholdPen, new Point(0, thresholdY), new Point(w, thresholdY));

        // Threshold label
        var thresholdLabel = new FormattedText($"{ThresholdValue} m/s\u00b2", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, ScreenMetrics.SmallFontPointSize, QgcColors.WarningBrush);
        context.DrawText(thresholdLabel, new Point(w - thresholdLabel.Width - 4, thresholdY - thresholdLabel.Height - 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 300 : Math.Min(availableSize.Width, 400);
        var h = double.IsInfinity(availableSize.Height) ? 280 : Math.Min(availableSize.Height, 400);
        return new Size(w, h);
    }
}

// ────────────────────────────────────────────────────────────────
// 5. LogDownloadPanel
//    QGC equivalent: AnalyzeView/LogDownloadPage.qml
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Log file list with size/date, download button, and delete button.
/// </summary>
public class LogDownloadPanel : TemplatedControl
{
    public static readonly StyledProperty<OnboardLogEntry?> SelectedLogProperty =
        AvaloniaProperty.Register<LogDownloadPanel, OnboardLogEntry?>(nameof(SelectedLog));

    public static readonly StyledProperty<double> DownloadProgressProperty =
        AvaloniaProperty.Register<LogDownloadPanel, double>(nameof(DownloadProgress), 0);

    public static readonly StyledProperty<bool> IsDownloadingProperty =
        AvaloniaProperty.Register<LogDownloadPanel, bool>(nameof(IsDownloading), false);

    public ObservableCollection<OnboardLogEntry> Logs { get; } = [];

    public OnboardLogEntry? SelectedLog { get => GetValue(SelectedLogProperty); set => SetValue(SelectedLogProperty, value); }
    public double DownloadProgress { get => GetValue(DownloadProgressProperty); set => SetValue(DownloadProgressProperty, value); }
    public bool IsDownloading { get => GetValue(IsDownloadingProperty); set => SetValue(IsDownloadingProperty, value); }

    public event EventHandler? RefreshRequested;
    public event EventHandler<OnboardLogEntry>? DownloadRequested;
    public event EventHandler<OnboardLogEntry>? DeleteRequested;
    public event EventHandler? DownloadAllRequested;
    public event EventHandler? EraseAllRequested;

    public void RequestRefresh() => RefreshRequested?.Invoke(this, EventArgs.Empty);

    public void RequestDownload()
    {
        if (SelectedLog is not null && !IsDownloading)
        {
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadRequested?.Invoke(this, SelectedLog);
        }
    }

    public void RequestDelete()
    {
        if (SelectedLog is not null && !IsDownloading)
            DeleteRequested?.Invoke(this, SelectedLog);
    }

    public void RequestDownloadAll()
    {
        if (!IsDownloading)
        {
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadAllRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RequestEraseAll()
    {
        if (!IsDownloading)
            EraseAllRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Formats a byte count for display (e.g. "1.2 MB").
    /// </summary>
    public static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}

// ────────────────────────────────────────────────────────────────
// 5. InstrumentValueEditDialog
//    QGC equivalent: QmlControls/InstrumentValueEditDialog.qml
//    Configures one telemetry slot on the instrument value bar:
//      - fact path (group + name), label override, units visibility,
//        decimal places, font size, and up to two color-threshold ranges.
// ────────────────────────────────────────────────────────────────

/// <summary>
/// A value-based color override entry for an instrument display cell.
/// When the cell's value exceeds <see cref="Threshold"/> the text is drawn
/// in <see cref="DisplayColor"/> instead of the default white.
/// </summary>
public sealed record InstrumentColorRange(double Threshold, Color DisplayColor);

/// <summary>
/// Modal dialog for editing a telemetry instrument value display cell.
/// Extends <see cref="PopupDialog"/> with properties that mirror
/// QGC's InstrumentValueEditDialog.qml.
/// </summary>
public class InstrumentValueEditDialog : PopupDialog
{
    // ── Fact path ──

    public static readonly StyledProperty<string> FactGroupProperty =
        AvaloniaProperty.Register<InstrumentValueEditDialog, string>(nameof(FactGroup), "");

    public static readonly StyledProperty<string> FactNameProperty =
        AvaloniaProperty.Register<InstrumentValueEditDialog, string>(nameof(FactName), "");

    // ── Display options ──

    public static readonly StyledProperty<string> LabelTextProperty =
        AvaloniaProperty.Register<InstrumentValueEditDialog, string>(nameof(LabelText), "");

    public static readonly StyledProperty<bool> ShowUnitsProperty =
        AvaloniaProperty.Register<InstrumentValueEditDialog, bool>(nameof(ShowUnits), true);

    public static readonly StyledProperty<int> DecimalPlacesProperty =
        AvaloniaProperty.Register<InstrumentValueEditDialog, int>(nameof(DecimalPlaces), 1);

    public static readonly StyledProperty<double> ValueFontSizeProperty =
        AvaloniaProperty.Register<InstrumentValueEditDialog, double>(nameof(ValueFontSize),
            ScreenMetrics.DefaultFontPointSize);

    // ── Color thresholds ──
    // Two optional ranges: Warning (yellow) and Error (red).

    public static readonly StyledProperty<InstrumentColorRange?> WarningRangeProperty =
        AvaloniaProperty.Register<InstrumentValueEditDialog, InstrumentColorRange?>(
            nameof(WarningRange));

    public static readonly StyledProperty<InstrumentColorRange?> ErrorRangeProperty =
        AvaloniaProperty.Register<InstrumentValueEditDialog, InstrumentColorRange?>(
            nameof(ErrorRange));

    // ── Properties ──

    public string FactGroup
    {
        get => GetValue(FactGroupProperty);
        set => SetValue(FactGroupProperty, value);
    }

    public string FactName
    {
        get => GetValue(FactNameProperty);
        set => SetValue(FactNameProperty, value);
    }

    public string LabelText
    {
        get => GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public bool ShowUnits
    {
        get => GetValue(ShowUnitsProperty);
        set => SetValue(ShowUnitsProperty, value);
    }

    public int DecimalPlaces
    {
        get => GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    public double ValueFontSize
    {
        get => GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }

    public InstrumentColorRange? WarningRange
    {
        get => GetValue(WarningRangeProperty);
        set => SetValue(WarningRangeProperty, value);
    }

    public InstrumentColorRange? ErrorRange
    {
        get => GetValue(ErrorRangeProperty);
        set => SetValue(ErrorRangeProperty, value);
    }

    // ── Constructor ──

    public InstrumentValueEditDialog()
    {
        DialogTitle      = "Edit Instrument Value";
        AcceptText       = "OK";
        CancelText       = "Cancel";
        ShowAcceptButton = true;
        ShowCancelButton = true;
    }

    // ── Helpers ──

    /// <summary>
    /// Returns the resolved text color for the given live <paramref name="value"/>,
    /// applying <see cref="ErrorRange"/> first then <see cref="WarningRange"/>.
    /// Falls back to <see cref="QgcColors.Text"/> if neither threshold is exceeded.
    /// </summary>
    public Color ResolveTextColor(double value)
    {
        if (ErrorRange is not null   && value >= ErrorRange.Threshold)   return ErrorRange.DisplayColor;
        if (WarningRange is not null && value >= WarningRange.Threshold) return WarningRange.DisplayColor;
        return QgcColors.Text;
    }

    /// <summary>
    /// Formats a raw double value to the configured number of decimal places.
    /// </summary>
    public string FormatValue(double value) =>
        value.ToString($"F{Math.Clamp(DecimalPlaces, 0, 6)}", CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns the full fact path string as "group.name" — matches QGC's
    /// InstrumentValue fact path convention.
    /// </summary>
    public string FactPath =>
        string.IsNullOrEmpty(FactGroup) ? FactName : $"{FactGroup}.{FactName}";
}

// ─────────────────────────────────────────────────────────────────────────────
// TelemetryChartPlaceholder  (#173 — OxyPlot chart placeholder)
// Renders a dashed-border panel with a "Install OxyPlot.Avalonia NuGet package
// to enable charts" message.  Replaced by a real chart control once OxyPlot
// is added to the project.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class TelemetryChartPlaceholder : Control
{
    public static readonly StyledProperty<string> ChartTitleProperty =
        AvaloniaProperty.Register<TelemetryChartPlaceholder, string>(nameof(ChartTitle), "Chart");
    public static readonly StyledProperty<string> ChartXLabelProperty =
        AvaloniaProperty.Register<TelemetryChartPlaceholder, string>(nameof(ChartXLabel), "Time (s)");
    public static readonly StyledProperty<string> ChartYLabelProperty =
        AvaloniaProperty.Register<TelemetryChartPlaceholder, string>(nameof(ChartYLabel), "Value");

    static TelemetryChartPlaceholder()
    {
        AffectsRender<TelemetryChartPlaceholder>(ChartTitleProperty, ChartXLabelProperty, ChartYLabelProperty);
    }

    public string ChartTitle  { get => GetValue(ChartTitleProperty);  set => SetValue(ChartTitleProperty, value); }
    public string ChartXLabel { get => GetValue(ChartXLabelProperty); set => SetValue(ChartXLabelProperty, value); }
    public string ChartYLabel { get => GetValue(ChartYLabelProperty); set => SetValue(ChartYLabelProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);

        // Background + dashed border
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, bounds);
        ctx.DrawRectangle(null,
            new Pen(new SolidColorBrush(QgcColors.ColorGrey), 1) { DashStyle = DashStyle.Dash },
            bounds.Deflate(2));

        // Title at top
        var ftTitle = new FormattedText(ChartTitle, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh,
            new SolidColorBrush(QgcColors.Text));
        ctx.DrawText(ftTitle, new Point((bounds.Width - ftTitle.Width) / 2, dfh * 0.5));

        // Placeholder message centred
        const string msg = "Install OxyPlot.Avalonia NuGet package to enable charts";
        var ftMsg = new FormattedText(msg, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(QgcColors.TextSecondary));
        ctx.DrawText(ftMsg, new Point((bounds.Width - ftMsg.Width) / 2, (bounds.Height - ftMsg.Height) / 2));

        // Axis labels
        if (!string.IsNullOrEmpty(ChartXLabel))
        {
            var ftX = new FormattedText(ChartXLabel, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.TextSecondary));
            ctx.DrawText(ftX, new Point((bounds.Width - ftX.Width) / 2, bounds.Height - dfh * 1.2));
        }
        if (!string.IsNullOrEmpty(ChartYLabel))
        {
            var ftY = new FormattedText(ChartYLabel, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.TextSecondary));
            // Rotate Y label 90°
            using var _ = ctx.PushTransform(
                Matrix.CreateRotation(-Math.PI / 2) *
                Matrix.CreateTranslation(dfh * 1.2, bounds.Height / 2 + ftY.Width / 2));
            ctx.DrawText(ftY, new Point(0, 0));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width)  ? 300 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height;
        return new Size(w, h);
    }
}

// ── #192 LogReplayBar ─────────────────────────────────────────────────────────
public class LogReplayBar : Control
{
    public static readonly StyledProperty<double> LRBPositionRatioProperty =
        AvaloniaProperty.Register<LogReplayBar, double>("LRBPositionRatio", 0.0);
    public static readonly StyledProperty<double> LRBTotalSecsProperty =
        AvaloniaProperty.Register<LogReplayBar, double>("LRBTotalSecs", 0.0);
    public static readonly StyledProperty<bool>   LRBIsPlayingProperty =
        AvaloniaProperty.Register<LogReplayBar, bool>("LRBIsPlaying", false);
    public static readonly StyledProperty<double> LRBSpeedMultProperty =
        AvaloniaProperty.Register<LogReplayBar, double>("LRBSpeedMult", 1.0);

    public double LRBPositionRatio { get => GetValue(LRBPositionRatioProperty); set => SetValue(LRBPositionRatioProperty, value); }
    public double LRBTotalSecs     { get => GetValue(LRBTotalSecsProperty);     set => SetValue(LRBTotalSecsProperty, value); }
    public bool   LRBIsPlaying     { get => GetValue(LRBIsPlayingProperty);     set => SetValue(LRBIsPlayingProperty, value); }
    public double LRBSpeedMult     { get => GetValue(LRBSpeedMultProperty);     set => SetValue(LRBSpeedMultProperty, value); }

    public event EventHandler<double>? SeekRequested;
    public event EventHandler?         PlayPauseToggled;
    public event EventHandler?         SpeedChangeRequested;

    static LogReplayBar()
    {
        AffectsRender<LogReplayBar>(LRBPositionRatioProperty, LRBTotalSecsProperty,
            LRBIsPlayingProperty, LRBSpeedMultProperty);
    }

    private Rect _playPauseRect;
    private Rect _speedRect;
    private Rect _scrubRect;

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        // Background
        dc.FillRectangle(new SolidColorBrush(QgcColors.Window), new Rect(0, 0, w, h));
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(QgcColors.GroupBorder)),
            new Rect(0, 0, w, h), br);

        double btnH = h * 0.65;
        double btnY = (h - btnH) / 2;
        double btnW = dfh * 2.5;
        double pad  = 6;

        // Play/Pause button
        _playPauseRect = new Rect(pad, btnY, btnW, btnH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Button), null, _playPauseRect, br);
        if (LRBIsPlaying)
        {
            // Pause icon (two bars)
            double barW2 = btnW * 0.2;
            double iX    = pad + (btnW - barW2 * 2.5) / 2;
            double iY    = btnY + btnH * 0.2;
            double iH    = btnH * 0.6;
            dc.FillRectangle(new SolidColorBrush(QgcColors.ButtonText), new Rect(iX, iY, barW2, iH));
            dc.FillRectangle(new SolidColorBrush(QgcColors.ButtonText), new Rect(iX + barW2 * 1.5, iY, barW2, iH));
        }
        else
        {
            // Play triangle
            double tX = pad + btnW * 0.3;
            double tY = btnY + btnH * 0.2;
            var tGeo  = new StreamGeometry();
            using (var ctx = tGeo.Open())
            {
                ctx.BeginFigure(new Point(tX, tY), true);
                ctx.LineTo(new Point(tX, tY + btnH * 0.6));
                ctx.LineTo(new Point(tX + btnW * 0.45, tY + btnH * 0.3));
                ctx.EndFigure(true);
            }
            dc.DrawGeometry(new SolidColorBrush(QgcColors.ButtonText), null, tGeo);
        }

        // Speed label
        string speedStr = $"{LRBSpeedMult:F1}x";
        var speedFt = new FormattedText(speedStr, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(QgcColors.Text));
        double speedBtnW = speedFt.Width + 12;
        _speedRect = new Rect(pad + btnW + pad, btnY, speedBtnW, btnH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Button), null, _speedRect, br);
        dc.DrawText(speedFt, new Point(_speedRect.X + (_speedRect.Width - speedFt.Width) / 2,
            _speedRect.Y + (_speedRect.Height - speedFt.Height) / 2));

        // Elapsed / total time
        double elapsed = LRBTotalSecs * Math.Clamp(LRBPositionRatio, 0, 1);
        string timeStr = $"{FormatTime(elapsed)} / {FormatTime(LRBTotalSecs)}";
        var timeFt = new FormattedText(timeStr, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
            new SolidColorBrush(QgcColors.TextSecondary));
        double timeX = w - timeFt.Width - pad;
        dc.DrawText(timeFt, new Point(timeX, (h - timeFt.Height) / 2));

        // Scrub track
        double scrubX = _speedRect.Right + pad;
        double scrubW = timeX - scrubX - pad;
        double scrubH = h * 0.25;
        double scrubY = (h - scrubH) / 2;
        if (scrubW > 20)
        {
            _scrubRect = new Rect(scrubX, scrubY, scrubW, scrubH);
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, _scrubRect, scrubH / 2);
            double fillW = scrubW * Math.Clamp(LRBPositionRatio, 0, 1);
            if (fillW > 0)
                dc.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
                    new Rect(scrubX, scrubY, fillW, scrubH), scrubH / 2);
            // Thumb
            double thumbX = scrubX + fillW - scrubH / 2;
            dc.DrawEllipse(new SolidColorBrush(QgcColors.Text), null,
                new Point(thumbX + scrubH / 2, h / 2), scrubH * 0.7, scrubH * 0.7);
        }
        else
        {
            _scrubRect = new Rect(-1, -1, 0, 0);
        }
    }

    private static string FormatTime(double secs)
    {
        if (secs <= 0) return "0:00";
        int s = (int)secs % 60;
        int m = (int)secs / 60 % 60;
        int hh = (int)secs / 3600;
        return hh > 0 ? $"{hh}:{m:D2}:{s:D2}" : $"{m}:{s:D2}";
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        if (_playPauseRect.Contains(pos)) { PlayPauseToggled?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        else if (_speedRect.Contains(pos)) { SpeedChangeRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        else if (_scrubRect.Width > 0 && _scrubRect.Contains(pos))
        {
            double ratio = (pos.X - _scrubRect.X) / _scrubRect.Width;
            SeekRequested?.Invoke(this, Math.Clamp(ratio, 0, 1));
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 2.2);
    }
}

// ── #193 VibrationMonitorRow ──────────────────────────────────────────────────
public class VibrationMonitorRow : Control
{
    public static readonly StyledProperty<double> VibXProperty    = AvaloniaProperty.Register<VibrationMonitorRow, double>("VibX", 0.0);
    public static readonly StyledProperty<double> VibYProperty    = AvaloniaProperty.Register<VibrationMonitorRow, double>("VibY", 0.0);
    public static readonly StyledProperty<double> VibZProperty    = AvaloniaProperty.Register<VibrationMonitorRow, double>("VibZ", 0.0);
    public static readonly StyledProperty<int>    ClipXProperty   = AvaloniaProperty.Register<VibrationMonitorRow, int>("ClipX", 0);
    public static readonly StyledProperty<int>    ClipYProperty   = AvaloniaProperty.Register<VibrationMonitorRow, int>("ClipY", 0);
    public static readonly StyledProperty<int>    ClipZProperty   = AvaloniaProperty.Register<VibrationMonitorRow, int>("ClipZ", 0);

    public double VibX  { get => GetValue(VibXProperty);  set => SetValue(VibXProperty, value); }
    public double VibY  { get => GetValue(VibYProperty);  set => SetValue(VibYProperty, value); }
    public double VibZ  { get => GetValue(VibZProperty);  set => SetValue(VibZProperty, value); }
    public int    ClipX { get => GetValue(ClipXProperty); set => SetValue(ClipXProperty, value); }
    public int    ClipY { get => GetValue(ClipYProperty); set => SetValue(ClipYProperty, value); }
    public int    ClipZ { get => GetValue(ClipZProperty); set => SetValue(ClipZProperty, value); }

    static VibrationMonitorRow()
    {
        AffectsRender<VibrationMonitorRow>(VibXProperty, VibYProperty, VibZProperty,
            ClipXProperty, ClipYProperty, ClipZProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds  = Bounds;
        double w    = bounds.Width;
        double h    = bounds.Height;
        var dfh     = ScreenMetrics.DefaultFontPixelHeight;

        double rowH    = h / 3;
        double labelW  = dfh * 1.2;
        double clipW   = dfh * 2.5;
        double barX    = labelW + 4;
        double barMaxW = w - barX - clipW - 6;

        (double vib, int clip, string axis)[] axes = {
            (VibX, ClipX, "X"),
            (VibY, ClipY, "Y"),
            (VibZ, ClipZ, "Z"),
        };

        for (int i = 0; i < 3; i++)
        {
            var (vib, clip, axis) = axes[i];
            double y = i * rowH;

            // Axis label
            var axFt = new FormattedText(axis, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
                new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(axFt, new Point(0, y + (rowH - axFt.Height) / 2));

            // Bar track
            double barH = rowH * 0.4;
            double barY = y + (rowH - barH) / 2;
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
                new Rect(barX, barY, barMaxW, barH), 2);

            // Fill — max recommended 30 m/s², warn at 60, critical at 90
            double ratio = Math.Clamp(vib / 90.0, 0, 1);
            Color fillColor = vib < 30 ? QgcColors.ColorGreen
                            : vib < 60 ? QgcColors.ColorOrange
                                       : QgcColors.ColorRed;
            double fillW = ratio * barMaxW;
            if (fillW > 0)
                dc.DrawRectangle(new SolidColorBrush(fillColor), null,
                    new Rect(barX, barY, fillW, barH), 2);

            // Value text
            var valFt = new FormattedText($"{vib:F1}", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(QgcColors.Text));
            dc.DrawText(valFt, new Point(barX + 3, barY + (barH - valFt.Height) / 2));

            // Clip count
            if (clip > 0)
            {
                var clipFt = new FormattedText($"C:{clip}", System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                    new SolidColorBrush(QgcColors.ColorRed));
                dc.DrawText(clipFt, new Point(barX + barMaxW + 3, y + (rowH - clipFt.Height) / 2));
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 260 : availableSize.Width;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 3.6);
    }
}

// ── #196 MavlinkMessageRow ────────────────────────────────────────────────────
public class MavlinkMessageRow : Control
{
    public static readonly StyledProperty<int>    MLMsgIdProperty =
        AvaloniaProperty.Register<MavlinkMessageRow, int>("MLMsgId", 0);
    public static readonly StyledProperty<string> MLMsgNameProperty =
        AvaloniaProperty.Register<MavlinkMessageRow, string>("MLMsgName", string.Empty);
    public static readonly StyledProperty<double> MLMsgRateProperty =
        AvaloniaProperty.Register<MavlinkMessageRow, double>("MLMsgRate", 0.0);
    public static readonly StyledProperty<int>    MLMsgFieldCountProperty =
        AvaloniaProperty.Register<MavlinkMessageRow, int>("MLMsgFieldCount", 0);
    public static readonly StyledProperty<bool>   MLIsHighlightedProperty =
        AvaloniaProperty.Register<MavlinkMessageRow, bool>("MLIsHighlighted", false);

    public int    MLMsgId         { get => GetValue(MLMsgIdProperty);         set => SetValue(MLMsgIdProperty, value); }
    public string MLMsgName       { get => GetValue(MLMsgNameProperty);       set => SetValue(MLMsgNameProperty, value); }
    public double MLMsgRate       { get => GetValue(MLMsgRateProperty);       set => SetValue(MLMsgRateProperty, value); }
    public int    MLMsgFieldCount { get => GetValue(MLMsgFieldCountProperty); set => SetValue(MLMsgFieldCountProperty, value); }
    public bool   MLIsHighlighted { get => GetValue(MLIsHighlightedProperty); set => SetValue(MLIsHighlightedProperty, value); }

    static MavlinkMessageRow()
    {
        AffectsRender<MavlinkMessageRow>(MLMsgIdProperty, MLMsgNameProperty, MLMsgRateProperty,
            MLMsgFieldCountProperty, MLIsHighlightedProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        if (MLIsHighlighted)
            dc.FillRectangle(new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), new Rect(0, 0, w, h));

        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        double col1 = w * 0.08;   // ID
        double col2 = w * 0.44;   // Name
        double col3 = w * 0.72;   // Rate
        double col4 = w * 0.88;   // Fields

        // ID
        var idFt = new FormattedText($"{MLMsgId}", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(QgcColors.ColorBlue));
        dc.DrawText(idFt, new Point(4, (h - idFt.Height) / 2));

        // Name
        var nameFt = new FormattedText(MLMsgName, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.82,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(nameFt, new Point(col1 + 4, (h - nameFt.Height) / 2));

        // Rate
        string rateStr = MLMsgRate > 0 ? $"{MLMsgRate:F1} Hz" : "—";
        var rateFt = new FormattedText(rateStr, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(MLMsgRate > 0 ? QgcColors.ColorGreen : QgcColors.TextSecondary));
        dc.DrawText(rateFt, new Point(col2 + 4, (h - rateFt.Height) / 2));

        // Field count
        var fldFt = new FormattedText($"{MLMsgFieldCount} fields", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(fldFt, new Point(col3 + 4, (h - fldFt.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 1.6);
    }
}

// ── #197 LogDownloadRow ───────────────────────────────────────────────────────
public class LogDownloadRow : Control
{
    public static readonly StyledProperty<string> LDDateStrProperty =
        AvaloniaProperty.Register<LogDownloadRow, string>("LDDateStr", string.Empty);
    public static readonly StyledProperty<double> LDSizeMBProperty =
        AvaloniaProperty.Register<LogDownloadRow, double>("LDSizeMB", 0.0);
    public static readonly StyledProperty<bool>   LDIsDownloadingProperty =
        AvaloniaProperty.Register<LogDownloadRow, bool>("LDIsDownloading", false);
    public static readonly StyledProperty<double> LDProgressProperty =
        AvaloniaProperty.Register<LogDownloadRow, double>("LDProgress", 0.0);
    public static readonly StyledProperty<int>    LDIndexProperty =
        AvaloniaProperty.Register<LogDownloadRow, int>("LDIndex", 0);
    public static readonly StyledProperty<bool>   LDIsSelectedProperty =
        AvaloniaProperty.Register<LogDownloadRow, bool>("LDIsSelected", false);

    public string LDDateStr      { get => GetValue(LDDateStrProperty);      set => SetValue(LDDateStrProperty, value); }
    public double LDSizeMB       { get => GetValue(LDSizeMBProperty);       set => SetValue(LDSizeMBProperty, value); }
    public bool   LDIsDownloading{ get => GetValue(LDIsDownloadingProperty); set => SetValue(LDIsDownloadingProperty, value); }
    public double LDProgress     { get => GetValue(LDProgressProperty);     set => SetValue(LDProgressProperty, value); }
    public int    LDIndex        { get => GetValue(LDIndexProperty);        set => SetValue(LDIndexProperty, value); }
    public bool   LDIsSelected   { get => GetValue(LDIsSelectedProperty);   set => SetValue(LDIsSelectedProperty, value); }

    public event EventHandler? DownloadRequested;
    public event EventHandler? EraseRequested;

    static LogDownloadRow()
    {
        AffectsRender<LogDownloadRow>(LDDateStrProperty, LDSizeMBProperty, LDIsDownloadingProperty,
            LDProgressProperty, LDIndexProperty, LDIsSelectedProperty);
    }

    private Rect _dlRect;
    private Rect _eraseRect;

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;

        if (LDIsSelected)
            dc.FillRectangle(new SolidColorBrush(Color.FromArgb(22, 30, 120, 255)), new Rect(0, 0, w, h));

        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Index
        var idxFt = new FormattedText($"{LDIndex}", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(idxFt, new Point(4, (h - idxFt.Height) / 2));

        // Date
        var dateFt = new FormattedText(LDDateStr, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.82,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(dateFt, new Point(w * 0.08 + 4, (h - dateFt.Height) / 2));

        // Size
        string sizeStr = LDSizeMB < 1 ? $"{LDSizeMB * 1024:F0} KB" : $"{LDSizeMB:F1} MB";
        var szFt = new FormattedText(sizeStr, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(szFt, new Point(w * 0.55 + 4, (h - szFt.Height) / 2));

        // Progress bar (if downloading)
        if (LDIsDownloading)
        {
            double barH = h * 0.25;
            double barX = w * 0.08 + 4;
            double barW = w * 0.46 - 8;
            double barY = h * 0.65;
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
                new Rect(barX, barY, barW, barH), 2);
            double fillW = barW * Math.Clamp(LDProgress, 0, 1);
            if (fillW > 0)
                dc.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
                    new Rect(barX, barY, fillW, barH), 2);
        }

        // Download / Erase buttons (right edge)
        double btnH = h * 0.65;
        double btnW = dfh * 2.8;
        double btnY = (h - btnH) / 2;

        _eraseRect = new Rect(w - btnW - 4, btnY, btnW, btnH);
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Button),
            new Pen(new SolidColorBrush(QgcColors.GroupBorder)), _eraseRect, br);
        var eraseFt = new FormattedText("Del", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
            new SolidColorBrush(QgcColors.ColorRed));
        dc.DrawText(eraseFt, new Point(_eraseRect.X + (_eraseRect.Width - eraseFt.Width) / 2,
            _eraseRect.Y + (_eraseRect.Height - eraseFt.Height) / 2));

        if (!LDIsDownloading)
        {
            _dlRect = new Rect(w - btnW * 2 - 8, btnY, btnW, btnH);
            dc.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null, _dlRect, br);
            var dlFt = new FormattedText("↓", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(QgcColors.ButtonText));
            dc.DrawText(dlFt, new Point(_dlRect.X + (_dlRect.Width - dlFt.Width) / 2,
                _dlRect.Y + (_dlRect.Height - dlFt.Height) / 2));
        }
        else
        {
            _dlRect = new Rect(-1, -1, 0, 0);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        if (_dlRect.Width > 0 && _dlRect.Contains(pos))    { DownloadRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        else if (_eraseRect.Contains(pos))                  { EraseRequested?.Invoke(this, EventArgs.Empty);   e.Handled = true; }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 2.0);
    }
}

// ── #224 MavlinkStatusBar ─────────────────────────────────────────────────────
// Compact MAVLink status row: system ID, component ID, protocol version,
// and a heartbeat pulse indicator (green blink when receiving).
// MSBSystemId, MSBComponentId, MSBProtocolVersion, MSBHeartbeatAge (seconds since last HB).
public sealed class MavlinkStatusBar : Control
{
    public static readonly StyledProperty<int>    MSBSystemIdProperty =
        AvaloniaProperty.Register<MavlinkStatusBar, int>("MSBSystemId", 1);
    public static readonly StyledProperty<int>    MSBComponentIdProperty =
        AvaloniaProperty.Register<MavlinkStatusBar, int>("MSBComponentId", 1);
    public static readonly StyledProperty<int>    MSBProtocolVersionProperty =
        AvaloniaProperty.Register<MavlinkStatusBar, int>("MSBProtocolVersion", 2);
    public static readonly StyledProperty<double> MSBHeartbeatAgeProperty =
        AvaloniaProperty.Register<MavlinkStatusBar, double>("MSBHeartbeatAge", 99.0);

    static MavlinkStatusBar()
    {
        AffectsRender<MavlinkStatusBar>(MSBSystemIdProperty, MSBComponentIdProperty,
            MSBProtocolVersionProperty, MSBHeartbeatAgeProperty);
    }

    public int    MSBSystemId        { get => GetValue(MSBSystemIdProperty);        set => SetValue(MSBSystemIdProperty, value); }
    public int    MSBComponentId     { get => GetValue(MSBComponentIdProperty);     set => SetValue(MSBComponentIdProperty, value); }
    public int    MSBProtocolVersion { get => GetValue(MSBProtocolVersionProperty); set => SetValue(MSBProtocolVersionProperty, value); }
    public double MSBHeartbeatAge    { get => GetValue(MSBHeartbeatAgeProperty);    set => SetValue(MSBHeartbeatAgeProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        // Background
        dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null, new Rect(0, 0, w, h));

        double x = dfw * 0.5;
        void DrawField(string label, string val)
        {
            var lbFt = new FormattedText(label,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.7, new SolidColorBrush(QgcColors.TextSecondary));
            var vlFt = new FormattedText(val,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
                dfh * 0.82, new SolidColorBrush(QgcColors.Text));
            dc.DrawText(lbFt, new Point(x, h / 2 - lbFt.Height));
            dc.DrawText(vlFt, new Point(x, h / 2));
            x += Math.Max(lbFt.Width, vlFt.Width) + dfw * 1.5;
        }

        DrawField("SYS",   MSBSystemId.ToString());
        DrawField("COMP",  MSBComponentId.ToString());
        DrawField("MAVv",  MSBProtocolVersion.ToString());

        // Heartbeat dot
        bool alive = MSBHeartbeatAge < 3.0;
        Color hbC  = alive ? QgcColors.ColorGreen : QgcColors.ColorRed;
        double dotR = h * 0.22;
        dc.DrawEllipse(new SolidColorBrush(hbC), null, new Point(x + dotR, h / 2), dotR, dotR);
        var hbFt = new FormattedText(alive ? $"{MSBHeartbeatAge:F1}s" : "LOST",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.7, new SolidColorBrush(hbC));
        dc.DrawText(hbFt, new Point(x + dotR * 2 + 4, (h - hbFt.Height) / 2));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width) ? 360 : available.Width;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight);
    }
}

// ── #225 GeoTagStatusRow ──────────────────────────────────────────────────────
// Single image entry in the GeoTag page: filename (left), GPS fix status dot +
// lat/lon text (right).  GTSRFilename, GTSRHasGps, GTSRLatitude, GTSRLongitude.
public sealed class GeoTagStatusRow : Control
{
    public static readonly StyledProperty<string> GTSRFilenameProperty =
        AvaloniaProperty.Register<GeoTagStatusRow, string>("GTSRFilename", string.Empty);
    public static readonly StyledProperty<bool>   GTSRHasGpsProperty =
        AvaloniaProperty.Register<GeoTagStatusRow, bool>("GTSRHasGps", false);
    public static readonly StyledProperty<double> GTSRLatitudeProperty =
        AvaloniaProperty.Register<GeoTagStatusRow, double>("GTSRLatitude", 0.0);
    public static readonly StyledProperty<double> GTSRLongitudeProperty =
        AvaloniaProperty.Register<GeoTagStatusRow, double>("GTSRLongitude", 0.0);

    static GeoTagStatusRow()
    {
        AffectsRender<GeoTagStatusRow>(GTSRFilenameProperty, GTSRHasGpsProperty,
            GTSRLatitudeProperty, GTSRLongitudeProperty);
    }

    public string GTSRFilename  { get => GetValue(GTSRFilenameProperty);  set => SetValue(GTSRFilenameProperty, value); }
    public bool   GTSRHasGps   { get => GetValue(GTSRHasGpsProperty);    set => SetValue(GTSRHasGpsProperty, value); }
    public double GTSRLatitude  { get => GetValue(GTSRLatitudeProperty);  set => SetValue(GTSRLatitudeProperty, value); }
    public double GTSRLongitude { get => GetValue(GTSRLongitudeProperty); set => SetValue(GTSRLongitudeProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;

        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Filename (left, truncated)
        var nameFt = new FormattedText(GTSRFilename,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.Text))
        { MaxTextWidth = w * 0.5 };
        dc.DrawText(nameFt, new Point(dfw * 0.4, (h - nameFt.Height) / 2));

        // GPS dot
        double dotR = h * 0.2;
        Color  dotC = GTSRHasGps ? QgcColors.ColorGreen : QgcColors.ColorGrey;
        dc.DrawEllipse(new SolidColorBrush(dotC), null, new Point(w * 0.55, h / 2), dotR, dotR);

        // Lat/lon
        string coord = GTSRHasGps
            ? $"{GTSRLatitude:F5}, {GTSRLongitude:F5}"
            : "No GPS";
        var coordFt = new FormattedText(coord,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.75, new SolidColorBrush(GTSRHasGps ? QgcColors.Text : QgcColors.TextSecondary));
        dc.DrawText(coordFt, new Point(w * 0.55 + dotR * 2 + 4, (h - coordFt.Height) / 2));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width) ? 420 : available.Width;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 2.0);
    }
}

// ── #238 FrequencyPlot ────────────────────────────────────────────────────────
// FFT bar chart placeholder for vibration/frequency analysis.
// FPBins is IReadOnlyList<double> (normalised 0–1 magnitudes); FPMaxFreqHz labels the X axis.
// Bars are coloured by magnitude: green→yellow→red.
public sealed class FrequencyPlot : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> FPBinsProperty =
        AvaloniaProperty.Register<FrequencyPlot, IReadOnlyList<double>?>("FPBins", null);
    public static readonly StyledProperty<double> FPMaxFreqHzProperty =
        AvaloniaProperty.Register<FrequencyPlot, double>("FPMaxFreqHz", 1000.0);
    public static readonly StyledProperty<string> FPTitleProperty =
        AvaloniaProperty.Register<FrequencyPlot, string>("FPTitle", string.Empty);

    static FrequencyPlot()
    {
        AffectsRender<FrequencyPlot>(FPBinsProperty, FPMaxFreqHzProperty, FPTitleProperty);
    }

    public IReadOnlyList<double>? FPBins     { get => GetValue(FPBinsProperty);     set => SetValue(FPBinsProperty, value); }
    public double                 FPMaxFreqHz{ get => GetValue(FPMaxFreqHzProperty);set => SetValue(FPMaxFreqHzProperty, value); }
    public string                 FPTitle    { get => GetValue(FPTitleProperty);    set => SetValue(FPTitleProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;

        // Background
        dc.DrawRectangle(new SolidColorBrush(QgcColors.Window), null, new Rect(0, 0, w, h));

        // Title
        double titleH = 0;
        if (!string.IsNullOrEmpty(FPTitle))
        {
            var tFt = new FormattedText(FPTitle,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.75, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(tFt, new Point(4, 2));
            titleH = tFt.Height + 4;
        }

        var bins = FPBins;
        if (bins == null || bins.Count == 0)
        {
            // Placeholder text
            var ph = new FormattedText("FFT data not available",
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.82, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(ph, new Point((w - ph.Width) / 2, (h - ph.Height) / 2));
            return;
        }

        double plotH = h - titleH - dfh * 1.2;
        double barW  = w / bins.Count;

        for (int i = 0; i < bins.Count; i++)
        {
            double mag   = Math.Clamp(bins[i], 0.0, 1.0);
            double barH2 = mag * plotH;
            double barX  = i * barW;
            double barY  = titleH + plotH - barH2;

            // Colour: green (low) → yellow → red (high)
            byte r = (byte)(mag < 0.5 ? mag * 2 * 255 : 255);
            byte g = (byte)(mag < 0.5 ? 255 : (1 - mag) * 2 * 255);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(r, g, 0)), null,
                new Rect(barX + 0.5, barY, Math.Max(barW - 1, 1), barH2));
        }

        // X-axis label
        var xFt = new FormattedText($"0 – {FPMaxFreqHz:F0} Hz",
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.65, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(xFt, new Point((w - xFt.Width) / 2, h - xFt.Height));
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 360 : available.Width;
        double h = double.IsInfinity(available.Height) ? 120 : available.Height;
        return new Size(w, h);
    }
}
