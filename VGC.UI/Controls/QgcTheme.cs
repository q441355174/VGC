using Avalonia;
using Avalonia.Media;
using System;

namespace VGC.Views.Controls;

/// <summary>
/// QGC-equivalent color palette (QGCPalette). Dark theme colors matching QGC's default dark mode.
/// All colors derived from QGC source: src/QmlControls/QGCPalette.cc
/// </summary>
public static class QgcColors
{
    // Window / Background
    public static readonly Color Window = Color.Parse("#222222");
    public static readonly Color WindowShade = Color.Parse("#333333");
    public static readonly Color WindowShadeDark = Color.Parse("#282828");
    public static readonly Color WindowShadeLight = Color.Parse("#3a3a3a");

    // Brand
    public static readonly Color BrandingPurple = Color.Parse("#4a2c6d");
    public static readonly Color BrandingBlue = Color.Parse("#48d6ff");

    // Toolbar
    public static readonly Color ToolbarBackground = Color.Parse("#222222");

    // Buttons
    public static readonly Color Button = Color.Parse("#464646");
    public static readonly Color ButtonText = Color.Parse("#ffffff");
    public static readonly Color ButtonHighlight = Color.Parse("#48d6ff");
    public static readonly Color ButtonHighlightText = Color.Parse("#000000");
    public static readonly Color PrimaryButton = Color.Parse("#585858");
    public static readonly Color PrimaryButtonText = Color.Parse("#ffffff");
    // Convenience aliases matching QGC usage patterns
    public static readonly Color ButtonFill = Color.Parse("#3d3d3d");         // standard button background
    public static readonly Color PrimaryButtonFill = Color.Parse("#1a4a70");  // hover / active button
    public static readonly Color DisabledText = Color.Parse("#505050");       // disabled label color

    // Text
    public static readonly Color Text = Color.Parse("#ffffff");
    public static readonly Color TextSecondary = Color.Parse("#909090");
    public static readonly Color WarningText = Color.Parse("#f9d838");
    public static readonly Color ErrorText = Color.Parse("#fb4f45");

    // Status colors
    public static readonly Color ColorGreen = Color.Parse("#17b93e");
    public static readonly Color ColorOrange = Color.Parse("#f19b16");
    public static readonly Color ColorRed = Color.Parse("#fb4f45");
    public static readonly Color ColorGrey = Color.Parse("#909090");
    public static readonly Color ColorBlue = Color.Parse("#48d6ff");

    // Alert
    public static readonly Color AlertBackground = Color.Parse("#b33535");
    public static readonly Color AlertText = Color.Parse("#ffffff");

    // Map
    public static readonly Color MapBackground = Color.Parse("#000000");
    public static readonly Color MissionItemEditor = Color.Parse("#585858");

    // Group / Section
    public static readonly Color GroupBorder = Color.Parse("#4a4a4a");

    // Transparent variants
    public static readonly Color WindowTransparent = Color.FromArgb(204, 34, 34, 34); // 0.8 opacity

    // Brushes (frequently used)
    public static readonly IBrush WindowBrush = new SolidColorBrush(Window);
    public static readonly IBrush WindowShadeBrush = new SolidColorBrush(WindowShade);
    public static readonly IBrush ButtonBrush = new SolidColorBrush(Button);
    public static readonly IBrush ButtonTextBrush = new SolidColorBrush(ButtonText);
    public static readonly IBrush ButtonHighlightBrush = new SolidColorBrush(ButtonHighlight);
    public static readonly IBrush TextBrush = new SolidColorBrush(Text);
    public static readonly IBrush TextSecondaryBrush = new SolidColorBrush(TextSecondary);
    public static readonly IBrush WarningBrush = new SolidColorBrush(WarningText);
    public static readonly IBrush ErrorBrush = new SolidColorBrush(ErrorText);
    public static readonly IBrush GreenBrush = new SolidColorBrush(ColorGreen);
    public static readonly IBrush RedBrush = new SolidColorBrush(ColorRed);
    public static readonly IBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);
}

/// <summary>
/// QGC-equivalent screen sizing utilities (ScreenTools).
/// Base unit sizes used throughout QGC for responsive layout.
/// </summary>
public static class ScreenLayout
{
    public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    public static double RightPanelWidth(double availableWidth) => Clamp(availableWidth / 3.0, ScreenMetrics.DefaultFontPixelWidth * 22, ScreenMetrics.RightPanelMaxWidth);

    public static Thickness ToolDrawerMargin(double topInset = 0) => new(0, topInset + ScreenMetrics.StandardMargin, ScreenMetrics.StandardMargin, ScreenMetrics.StandardMargin);

    public static Thickness IndicatorDrawerMargin(double topInset = 0) => new(0, topInset + ScreenMetrics.ToolbarHeight + ScreenMetrics.StandardMargin, ScreenMetrics.StandardMargin, 0);

    public static Thickness ToolSelectMargin(double topInset = 0) => new(ScreenMetrics.StandardMargin, topInset + ScreenMetrics.ToolbarHeight + ScreenMetrics.StandardMargin, 0, 0);

    public static double MapScaleOffsetX() => ScreenMetrics.ToolStripWidth + ScreenMetrics.ToolsMargin;
}

public static class ScreenMetrics
{
    // Base font metrics (QGC defaults at 96 DPI)
    public const double DefaultFontPixelWidth = 10;
    public const double DefaultFontPixelHeight = 16;
    public const double LargeFontPixelWidth = 12;
    public const double LargeFontPixelHeight = 20;
    public const double SmallFontPointSize = 8;
    public const double DefaultFontPointSize = 10;
    public const double MediumFontPointSize = 12.5;
    public const double LargeFontPointSize = 15;

    // Derived metrics matching QGC ScreenTools
    public const double ToolbarHeight = 48;
    public const double DefaultBorderRadius = 5;
    public const double ImplicitButtonWidth = 50;  // 5 * DFW
    public const double ImplicitButtonHeight = 26; // 1.6 * DFH
    public const double ImplicitCheckBoxHeight = 16;
    public const double ImplicitComboBoxHeight = 26;
    public const double ComboBoxPadding = 4;

    // Layout margins matching QGC
    public const double StandardMargin = 5;        // DFW * 0.5
    public const double ToolsMargin = 7.5;         // DFW * 0.75
    public const double WidgetMargin = 7.5;        // DFW * 0.75
    public const double LayoutMargin = 7.5;        // DFW * 0.75

    // Component sizes
    public const double ToolStripWidth = 70;       // DFW * 7
    public const double RightPanelMaxWidth = 300;  // DFW * 30
    public const double ToolStripButtonSpacing = 2.5; // DFW * 0.25
    public const double ToolStripPadding = 4;      // DFW * 0.4
    public const double MajorTickWidth = 24;       // LFW * 2
    public const double MajorTickHeight = 40;      // LFH * 2
    public const double JoystickHatWidth = 16;     // DFH
    public const double PipDefaultRatio = 0.2;     // 20% of parent
    public const double PipMinRatio = 0.1;
    public const double PipMaxRatio = 0.75;
    public const double PipAspectRatio = 9.0 / 16.0; // 16:9
    public const double VirtualJoystickMaxHeightRatio = 0.25;
    public const double VirtualJoystickMaxPixels = 160; // DFW * 16
}
