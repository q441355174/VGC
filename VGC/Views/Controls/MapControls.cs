using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using Mapsui.Extensions;
using Mapsui.Projections;

namespace VGC.Views.Controls;

// ════════════════════════════════════════════════════════════════
// MAP DATA MODELS
// ════════════════════════════════════════════════════════════════

/// <summary>Geographic coordinate for map items.</summary>
public sealed record MapGeoPoint(double Latitude, double Longitude, double Altitude = 0);

/// <summary>Vehicle marker on map.</summary>
public sealed record VehicleMapMarker(
    MapGeoPoint Position,
    double HeadingDeg,
    double GimbalYawDeg,
    string Label,
    bool IsActive,
    Color Color);

/// <summary>Mission waypoint marker.</summary>
public sealed record MissionMarker(
    int Sequence,
    MapGeoPoint Position,
    string CommandName,
    bool IsCurrent);

/// <summary>Geofence circle.</summary>
public sealed record MapCircleOverlay(
    MapGeoPoint Center,
    double RadiusMeters,
    bool IsInclusion,
    Color StrokeColor,
    Color FillColor);

/// <summary>Geofence or survey polygon.</summary>
public sealed record MapPolygonOverlay(
    IReadOnlyList<MapGeoPoint> Vertices,
    bool IsInclusion,
    Color StrokeColor,
    Color FillColor);

/// <summary>Mission path or corridor polyline.</summary>
public sealed record MapPolylineOverlay(
    IReadOnlyList<MapGeoPoint> Points,
    Color StrokeColor,
    double StrokeWidth);

/// <summary>Rally point marker.</summary>
public sealed record RallyPointMarker(
    MapGeoPoint Position,
    string Label);

/// <summary>Camera trigger location marker.</summary>
public sealed record CameraTriggerMarker(MapGeoPoint Position, int PhotoIndex);

// ════════════════════════════════════════════════════════════════
// MAP ABSTRACTIONS (implementation-agnostic)
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Map provider abstraction. Implementations plug in Mapsui, GMap.NET, or other map libraries.
/// </summary>
public interface IMapRenderer
{
    /// <summary>Set map center and zoom.</summary>
    void SetViewport(MapGeoPoint center, double zoomLevel);

    /// <summary>Convert screen coordinates to geographic.</summary>
    MapGeoPoint? ScreenToGeo(double screenX, double screenY, double controlWidth, double controlHeight);

    /// <summary>Convert geographic coordinates to screen position.</summary>
    (double X, double Y)? GeoToScreen(MapGeoPoint point, double controlWidth, double controlHeight);

    /// <summary>Render map tiles onto drawing context.</summary>
    void RenderTiles(DrawingContext context, Rect bounds);
}

/// <summary>
/// Mapsui-backed geographic renderer. Uses Mapsui's SphericalMercator projection for
/// QGC-equivalent screen/geo math and renders a vector fallback until tile painting is wired.
/// </summary>
public sealed class MapsuiMapRenderer : IMapRenderer
{
    private const int TileSize = 256;
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { UserAgent = { new("VGC", "1.0") } }
    };
    private static readonly string TileCacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VGC", "maps", "osm");
    private static readonly ConcurrentDictionary<string, Bitmap> TileBitmaps = new();
    private static readonly ConcurrentDictionary<string, Task> TileRequests = new();

    private MapGeoPoint _center = new(0, 0);
    private double _zoom = 15;

    public void SetViewport(MapGeoPoint center, double zoomLevel)
    {
        _center = new MapGeoPoint(ClampLatitude(center.Latitude), WrapLongitude(center.Longitude), center.Altitude);
        _zoom = Math.Clamp(zoomLevel, 1, 19);
    }

    public MapGeoPoint? ScreenToGeo(double screenX, double screenY, double controlWidth, double controlHeight)
    {
        if (controlWidth <= 0 || controlHeight <= 0) return null;
        var zoom = IntegralZoom(_zoom);
        var centerPixel = LonLatToPixel(_center.Longitude, _center.Latitude, zoom);
        var worldPixel = (X: centerPixel.X + screenX - controlWidth / 2, Y: centerPixel.Y + screenY - controlHeight / 2);
        var lonLat = PixelToLonLat(worldPixel.X, worldPixel.Y, zoom);
        return new MapGeoPoint(ClampLatitude(lonLat.Latitude), WrapLongitude(lonLat.Longitude));
    }

    public (double X, double Y)? GeoToScreen(MapGeoPoint point, double controlWidth, double controlHeight)
    {
        if (controlWidth <= 0 || controlHeight <= 0) return null;
        var zoom = IntegralZoom(_zoom);
        var centerPixel = LonLatToPixel(_center.Longitude, _center.Latitude, zoom);
        var pointPixel = LonLatToPixel(WrapLongitude(point.Longitude), ClampLatitude(point.Latitude), zoom);
        return (controlWidth / 2 + pointPixel.X - centerPixel.X, controlHeight / 2 + pointPixel.Y - centerPixel.Y);
    }

    public void RenderTiles(DrawingContext context, Rect bounds)
    {
        DrawBackground(context, bounds);
        var zoom = IntegralZoom(_zoom);
        var centerPixel = LonLatToPixel(_center.Longitude, _center.Latitude, zoom);
        var topLeft = (X: centerPixel.X - bounds.Width / 2, Y: centerPixel.Y - bounds.Height / 2);
        var minTileX = (int)Math.Floor(topLeft.X / TileSize);
        var minTileY = (int)Math.Floor(topLeft.Y / TileSize);
        var maxTileX = (int)Math.Floor((topLeft.X + bounds.Width) / TileSize);
        var maxTileY = (int)Math.Floor((topLeft.Y + bounds.Height) / TileSize);
        var maxIndex = (1 << zoom) - 1;

        for (var ty = minTileY; ty <= maxTileY; ty++)
        {
            if (ty < 0 || ty > maxIndex) continue;
            for (var tx = minTileX; tx <= maxTileX; tx++)
            {
                var wrappedX = ((tx % (maxIndex + 1)) + maxIndex + 1) % (maxIndex + 1);
                var x = tx * TileSize - topLeft.X;
                var y = ty * TileSize - topLeft.Y;
                var dest = new Rect(x, y, TileSize, TileSize);
                var key = TileKey(zoom, wrappedX, ty);
                if (TileBitmaps.TryGetValue(key, out var bitmap))
                    context.DrawImage(bitmap, dest);
                else
                {
                    DrawLoadingTile(context, dest, zoom, wrappedX, ty);
                    QueueTileLoad(zoom, wrappedX, ty, key);
                }
            }
        }
    }

    private static void DrawBackground(DrawingContext context, Rect bounds)
    {
        var bg = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#0d1824"), 0),
                new GradientStop(Color.Parse("#102334"), 1)
            }
        };
        context.DrawRectangle(bg, null, bounds);
    }

    private static void DrawLoadingTile(DrawingContext context, Rect dest, int zoom, int x, int y)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(36, 72, 214, 255)),
            new Pen(new SolidColorBrush(Color.FromArgb(45, 72, 214, 255)), 1), dest);
        var text = new FormattedText($"{zoom}/{x}/{y}", CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(QgcColors.TextSecondary));
        context.DrawText(text, new Point(dest.X + 6, dest.Y + 6));
    }

    private static void QueueTileLoad(int zoom, int x, int y, string key)
    {
        TileRequests.GetOrAdd(key, _ => Task.Run(async () =>
        {
            try
            {
                var path = TilePath(zoom, x, y);
                byte[] bytes;
                if (File.Exists(path))
                    bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
                else
                {
                    bytes = await HttpClient.GetByteArrayAsync($"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png").ConfigureAwait(false);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!TileBitmaps.ContainsKey(key))
                        TileBitmaps[key] = new Bitmap(new MemoryStream(bytes));
                });
            }
            catch
            {
                // Keep the loading tile visible; later renders can retry after request removal.
            }
            finally
            {
                TileRequests.TryRemove(key, out Task? _);
            }
        }));
    }

    private static string TileKey(int zoom, int x, int y) => $"{zoom}/{x}/{y}";
    private static string TilePath(int zoom, int x, int y) => Path.Combine(TileCacheRoot, zoom.ToString(CultureInfo.InvariantCulture), x.ToString(CultureInfo.InvariantCulture), y + ".png");
    private static int IntegralZoom(double zoom) => Math.Clamp((int)Math.Round(zoom), 1, 19);
    private static double ClampLatitude(double latitude) => Math.Clamp(latitude, -85.05112878, 85.05112878);
    private static double WrapLongitude(double longitude) => ((longitude + 540) % 360) - 180;

    private static (double X, double Y) LonLatToPixel(double lon, double lat, int zoom)
    {
        var projected = SphericalMercator.FromLonLat(lon, lat).ToMPoint();
        var originShift = 20037508.342789244;
        var mapSize = TileSize * Math.Pow(2, zoom);
        var x = (projected.X + originShift) / (2 * originShift) * mapSize;
        var y = (originShift - projected.Y) / (2 * originShift) * mapSize;
        return (x, y);
    }

    private static (double Longitude, double Latitude) PixelToLonLat(double x, double y, int zoom)
    {
        var originShift = 20037508.342789244;
        var mapSize = TileSize * Math.Pow(2, zoom);
        var mx = x / mapSize * (2 * originShift) - originShift;
        var my = originShift - y / mapSize * (2 * originShift);
        var lonLat = SphericalMercator.ToLonLat(mx, my);
        return (lonLat.lon, lonLat.lat);
    }
}

// ════════════════════════════════════════════════════════════════
// FLIGHTMAP CONTROL
// QGC equivalent: FlightMap/FlightMap.qml
// Main map surface with overlays, markers, interaction
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Main flight map control — renders map tiles + all overlays (vehicles, waypoints, fences, paths).
/// Equivalent to QGC FlightMap.qml. Delegates tile rendering to <see cref="IMapRenderer"/>.
/// </summary>
public sealed class FlightMapControl : Control
{
    private IMapRenderer _renderer;

    public static readonly StyledProperty<MapGeoPoint> CenterProperty =
        AvaloniaProperty.Register<FlightMapControl, MapGeoPoint>(nameof(Center), new MapGeoPoint(0, 0));

    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<FlightMapControl, double>(nameof(ZoomLevel), 15);

    public static readonly StyledProperty<bool> FollowVehicleProperty =
        AvaloniaProperty.Register<FlightMapControl, bool>(nameof(FollowVehicle), true);

    static FlightMapControl()
    {
        AffectsRender<FlightMapControl>(CenterProperty, ZoomLevelProperty);
    }

    public FlightMapControl()
    {
        _renderer = new MapsuiMapRenderer();
        Vehicles = [];
        MissionPath = [];
        Waypoints = [];
        Circles = [];
        Polygons = [];
        Polylines = [];
        RallyPoints = [];
        CameraTriggers = [];
    }

    public MapGeoPoint Center { get => GetValue(CenterProperty); set { SetValue(CenterProperty, value); _renderer.SetViewport(value, ZoomLevel); } }
    public double ZoomLevel { get => GetValue(ZoomLevelProperty); set { SetValue(ZoomLevelProperty, value); _renderer.SetViewport(Center, value); } }
    public bool FollowVehicle { get => GetValue(FollowVehicleProperty); set => SetValue(FollowVehicleProperty, value); }

    // Overlay collections
    public ObservableCollection<VehicleMapMarker> Vehicles { get; }
    public ObservableCollection<MapGeoPoint> MissionPath { get; }
    public ObservableCollection<MissionMarker> Waypoints { get; }
    public ObservableCollection<MapCircleOverlay> Circles { get; }
    public ObservableCollection<MapPolygonOverlay> Polygons { get; }
    public ObservableCollection<MapPolylineOverlay> Polylines { get; }
    public ObservableCollection<RallyPointMarker> RallyPoints { get; }
    public ObservableCollection<CameraTriggerMarker> CameraTriggers { get; }

    // Events
    public event EventHandler<MapGeoPoint>? MapClicked;
    public event EventHandler<MapGeoPoint>? MapLongPressed { add { } remove { } }

    /// <summary>Replace the map renderer (call when Mapsui/GMap is available).</summary>
    public void SetRenderer(IMapRenderer renderer)
    {
        _renderer = renderer;
        _renderer.SetViewport(Center, ZoomLevel);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 1 || bounds.Height < 1) return;

        // 1. Map tiles
        _renderer.RenderTiles(context, new Rect(0, 0, bounds.Width, bounds.Height));

        // 2. Polylines (mission paths, corridors)
        foreach (var pl in Polylines)
            RenderPolyline(context, pl, bounds);

        // 3. Polygons (geofence)
        foreach (var pg in Polygons)
            RenderPolygon(context, pg, bounds);

        // 4. Circles (geofence)
        foreach (var c in Circles)
            RenderCircle(context, c, bounds);

        // 5. Mission path line
        if (MissionPath.Count > 1)
            RenderMissionPath(context, bounds);

        // 6. Camera triggers
        foreach (var ct in CameraTriggers)
            RenderCameraTrigger(context, ct, bounds);

        // 7. Rally points
        foreach (var rp in RallyPoints)
            RenderRallyPoint(context, rp, bounds);

        // 8. Waypoints
        foreach (var wp in Waypoints)
            RenderWaypoint(context, wp, bounds);

        // 9. Vehicles (on top)
        foreach (var v in Vehicles)
            RenderVehicle(context, v, bounds);

        // 10. Center crosshair when no vehicle
        if (Vehicles.Count == 0)
            RenderCrosshair(context, bounds);
    }

    private void RenderVehicle(DrawingContext context, VehicleMapMarker v, Rect bounds)
    {
        var pos = _renderer.GeoToScreen(v.Position, bounds.Width, bounds.Height);
        if (pos is null) return;

        var (x, y) = pos.Value;
        var size = 20.0;

        // Vehicle body
        using var _ = context.PushTransform(
            Matrix.CreateTranslation(-x, -y) *
            Matrix.CreateRotation(v.HeadingDeg * Math.PI / 180) *
            Matrix.CreateTranslation(x, y));

        var vehicleBrush = new SolidColorBrush(v.IsActive ? QgcColors.ColorGreen : QgcColors.ColorGrey);
        var vehiclePen = new Pen(Brushes.White, 2);

        // Triangle pointing up (nose direction)
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x, y - size), true);
            ctx.LineTo(new Point(x - size * 0.6, y + size * 0.5));
            ctx.LineTo(new Point(x + size * 0.6, y + size * 0.5));
            ctx.EndFigure(true);
        }
        context.DrawGeometry(vehicleBrush, vehiclePen, geometry);

        // Gimbal direction line
        if (Math.Abs(v.GimbalYawDeg) > 0.1)
        {
            var gimbalRad = v.GimbalYawDeg * Math.PI / 180;
            var gimbalPen = new Pen(new SolidColorBrush(QgcColors.ColorOrange), 1.5);
            context.DrawLine(gimbalPen,
                new Point(x, y),
                new Point(x + Math.Sin(gimbalRad) * size * 1.5, y - Math.Cos(gimbalRad) * size * 1.5));
        }

        // Label
        if (!string.IsNullOrEmpty(v.Label))
        {
            var tf = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
            var fmt = new FormattedText(v.Label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, 10, Brushes.White);
            context.DrawText(fmt, new Point(x - fmt.Width / 2, y + size * 0.7));
        }
    }

    private void RenderWaypoint(DrawingContext context, MissionMarker wp, Rect bounds)
    {
        var pos = _renderer.GeoToScreen(wp.Position, bounds.Width, bounds.Height);
        if (pos is null) return;

        var (x, y) = pos.Value;
        var radius = wp.IsCurrent ? 16.0 : 12.0;

        var fillBrush = new SolidColorBrush(wp.IsCurrent ? QgcColors.ColorBlue : Color.Parse("#f0c443"));
        var borderPen = new Pen(Brushes.White, 2);
        context.DrawEllipse(fillBrush, borderPen, new Point(x, y), radius, radius);

        // Sequence number
        var tf = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var numText = new FormattedText(wp.Sequence.ToString(), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, tf, 11, new SolidColorBrush(QgcColors.ButtonHighlightText));
        context.DrawText(numText, new Point(x - numText.Width / 2, y - numText.Height / 2));
    }

    private void RenderMissionPath(DrawingContext context, Rect bounds)
    {
        var pen = new Pen(new SolidColorBrush(QgcColors.ColorBlue), 2);
        for (var i = 0; i < MissionPath.Count - 1; i++)
        {
            var p1 = _renderer.GeoToScreen(MissionPath[i], bounds.Width, bounds.Height);
            var p2 = _renderer.GeoToScreen(MissionPath[i + 1], bounds.Width, bounds.Height);
            if (p1 is not null && p2 is not null)
                context.DrawLine(pen, new Point(p1.Value.X, p1.Value.Y), new Point(p2.Value.X, p2.Value.Y));
        }
    }

    private void RenderPolyline(DrawingContext context, MapPolylineOverlay pl, Rect bounds)
    {
        if (pl.Points.Count < 2) return;
        var pen = new Pen(new SolidColorBrush(pl.StrokeColor), pl.StrokeWidth);
        for (var i = 0; i < pl.Points.Count - 1; i++)
        {
            var p1 = _renderer.GeoToScreen(pl.Points[i], bounds.Width, bounds.Height);
            var p2 = _renderer.GeoToScreen(pl.Points[i + 1], bounds.Width, bounds.Height);
            if (p1 is not null && p2 is not null)
                context.DrawLine(pen, new Point(p1.Value.X, p1.Value.Y), new Point(p2.Value.X, p2.Value.Y));
        }
    }

    private void RenderPolygon(DrawingContext context, MapPolygonOverlay pg, Rect bounds)
    {
        if (pg.Vertices.Count < 3) return;
        var screenPoints = pg.Vertices
            .Select(v => _renderer.GeoToScreen(v, bounds.Width, bounds.Height))
            .Where(p => p is not null)
            .Select(p => new Point(p!.Value.X, p.Value.Y))
            .ToArray();
        if (screenPoints.Length < 3) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(screenPoints[0], true);
            for (var i = 1; i < screenPoints.Length; i++)
                ctx.LineTo(screenPoints[i]);
            ctx.EndFigure(true);
        }

        var fillBrush = new SolidColorBrush(pg.FillColor);
        var strokePen = new Pen(new SolidColorBrush(pg.StrokeColor), 2);
        context.DrawGeometry(fillBrush, strokePen, geometry);
    }

    private void RenderCircle(DrawingContext context, MapCircleOverlay c, Rect bounds)
    {
        var center = _renderer.GeoToScreen(c.Center, bounds.Width, bounds.Height);
        if (center is null) return;

        // Approximate radius in pixels (rough: 1 degree ≈ 111km at equator)
        var degreesPerPixel = 360.0 / (Math.Pow(2, ZoomLevel) * 256);
        var radiusDeg = c.RadiusMeters / 111000.0;
        var radiusPx = radiusDeg / degreesPerPixel;

        var fillBrush = new SolidColorBrush(c.FillColor);
        var strokePen = new Pen(new SolidColorBrush(c.StrokeColor), 2);
        context.DrawEllipse(fillBrush, strokePen, new Point(center.Value.X, center.Value.Y), radiusPx, radiusPx);
    }

    private void RenderRallyPoint(DrawingContext context, RallyPointMarker rp, Rect bounds)
    {
        var pos = _renderer.GeoToScreen(rp.Position, bounds.Width, bounds.Height);
        if (pos is null) return;

        var (x, y) = pos.Value;
        var brush = new SolidColorBrush(QgcColors.ColorOrange);
        var pen = new Pen(Brushes.White, 1.5);

        // Diamond shape
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x, y - 10), true);
            ctx.LineTo(new Point(x + 8, y));
            ctx.LineTo(new Point(x, y + 10));
            ctx.LineTo(new Point(x - 8, y));
            ctx.EndFigure(true);
        }
        context.DrawGeometry(brush, pen, geometry);

        if (!string.IsNullOrEmpty(rp.Label))
        {
            var tf = new Typeface("Segoe UI");
            var fmt = new FormattedText(rp.Label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, 9, Brushes.White);
            context.DrawText(fmt, new Point(x - fmt.Width / 2, y + 12));
        }
    }

    private void RenderCameraTrigger(DrawingContext context, CameraTriggerMarker ct, Rect bounds)
    {
        var pos = _renderer.GeoToScreen(ct.Position, bounds.Width, bounds.Height);
        if (pos is null) return;

        var (x, y) = pos.Value;
        var brush = new SolidColorBrush(Color.Parse("#e91e63"));
        context.DrawEllipse(brush, null, new Point(x, y), 5, 5);
    }

    private static void RenderCrosshair(DrawingContext context, Rect bounds)
    {
        var cx = bounds.Width / 2;
        var cy = bounds.Height / 2;
        var pen = new Pen(new SolidColorBrush(Color.Parse("#405567")), 1);
        context.DrawLine(pen, new Point(cx - 20, cy), new Point(cx + 20, cy));
        context.DrawLine(pen, new Point(cx, cy - 20), new Point(cx, cy + 20));
    }

    // Interaction
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var geo = _renderer.ScreenToGeo(pos.X, pos.Y, Bounds.Width, Bounds.Height);
        if (geo is not null)
            MapClicked?.Invoke(this, geo);
    }
}

// ════════════════════════════════════════════════════════════════
// MAP SCALE CONTROL
// QGC equivalent: FlightMap/MapScale.qml
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Map scale bar indicator showing distance at current zoom level.
/// Equivalent to QGC FlightMap/MapScale.qml
/// </summary>
public sealed class MapScaleControl : Control
{
    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<MapScaleControl, double>(nameof(ZoomLevel), 15);

    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<MapScaleControl, double>(nameof(Latitude), 0);

    static MapScaleControl()
    {
        AffectsRender<MapScaleControl>(ZoomLevelProperty, LatitudeProperty);
    }

    public double ZoomLevel { get => GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }
    public double Latitude { get => GetValue(LatitudeProperty); set => SetValue(LatitudeProperty, value); }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width < 20 || bounds.Height < 10) return;

        // Calculate meters per pixel at this zoom and latitude
        var metersPerPixel = 156543.03392 * Math.Cos(Latitude * Math.PI / 180) / Math.Pow(2, ZoomLevel);
        if (metersPerPixel <= 0) return;

        // Find a nice round distance that fits in ~100px
        var targetPixels = 100.0;
        var targetMeters = metersPerPixel * targetPixels;

        // Round to nice values
        double[] niceValues = [1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000];
        var scaleMeters = niceValues[0];
        foreach (var v in niceValues)
        {
            if (v >= targetMeters * 0.5)
            {
                scaleMeters = v;
                break;
            }
        }

        var scalePixels = scaleMeters / metersPerPixel;
        var scaleText = scaleMeters >= 1000 ? $"{scaleMeters / 1000:F0} km" : $"{scaleMeters:F0} m";

        var y = bounds.Height - 4;
        var x = 4.0;
        var barHeight = ScreenMetrics.DefaultFontPixelHeight;

        var color = new SolidColorBrush(Colors.White);
        var pen = new Pen(color, 2);

        // Left end
        context.DrawLine(pen, new Point(x, y), new Point(x, y - barHeight));
        // Horizontal bar
        context.DrawLine(pen, new Point(x, y - 2), new Point(x + scalePixels, y - 2));
        // Right end
        context.DrawLine(pen, new Point(x + scalePixels, y), new Point(x + scalePixels, y - barHeight));

        // Label
        var tf = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var fmt = new FormattedText(scaleText, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, tf, 11, color);
        context.DrawText(fmt, new Point(x + scalePixels + 4, y - barHeight));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(
            double.IsInfinity(availableSize.Width) ? 180 : Math.Min(availableSize.Width, 180),
            double.IsInfinity(availableSize.Height) ? 30 : Math.Min(availableSize.Height, 30));
    }
}

// ════════════════════════════════════════════════════════════════
// MAP FIT HELPER
// QGC equivalent: FlightMap/Widgets/MapFitFunctions.qml
// Computes viewport center+zoom to enclose sets of geographic coordinates.
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Utility class for fitting the map viewport to geographic coordinate sets.
/// Equivalent to QGC FlightMap/Widgets/MapFitFunctions.qml.
/// All methods return a (Center, ZoomLevel) tuple consumed by FlightMapControl.
/// </summary>
public static class MapFitHelper
{
    private const double MinDegreeSpan = 0.001; // ~110 m minimum span

    /// <summary>
    /// Compute viewport (center + zoom) that encloses all provided coordinates with a 20% margin.
    /// Returns null when the list is empty or contains only invalid (NaN/zero) points.
    /// </summary>
    public static (MapGeoPoint Center, double ZoomLevel)? FitToCoordinates(
        IReadOnlyList<MapGeoPoint> coordinates,
        double controlWidth  = 800,
        double controlHeight = 600)
    {
        if (coordinates.Count == 0) return null;

        var valid = coordinates
            .Where(c => !double.IsNaN(c.Latitude) && !double.IsNaN(c.Longitude)
                        && (c.Latitude != 0.0 || c.Longitude != 0.0))
            .ToList();

        if (valid.Count == 0) return null;
        if (valid.Count == 1) return (valid[0], 14.0);

        var north = valid.Max(c => c.Latitude);
        var south = valid.Min(c => c.Latitude);
        var east  = valid.Max(c => c.Longitude);
        var west  = valid.Min(c => c.Longitude);

        // Enforce minimum span and add 10% margin on each side (20% total)
        var latSpan = Math.Max(north - south, MinDegreeSpan);
        var lonSpan = Math.Max(east  - west,  MinDegreeSpan);
        north += latSpan * 0.1;
        south -= latSpan * 0.1;
        east  += lonSpan * 0.1;
        west  -= lonSpan * 0.1;

        var center = new MapGeoPoint((north + south) / 2, (east + west) / 2);

        // Web Mercator tile formula: zoom = log2(controlSize * 360 / (256 * span)) - 1 (padding)
        var latZoom = Math.Log2(controlHeight * 360.0 / (256.0 * (north - south)));
        var lonZoom = Math.Log2(controlWidth  * 360.0 / (256.0 * (east  - west)));
        var zoom    = Math.Clamp(Math.Floor(Math.Min(latZoom, lonZoom)) - 1, 2, 20);

        return (center, zoom);
    }

    /// <summary>Fit viewport to a mission flight path.</summary>
    public static (MapGeoPoint Center, double ZoomLevel)? FitToMissionPath(
        IEnumerable<MapGeoPoint> path,
        double controlWidth  = 800,
        double controlHeight = 600) =>
        FitToCoordinates(path.ToList(), controlWidth, controlHeight);

    /// <summary>
    /// Fit viewport to all map overlays simultaneously (waypoints, geofence circles,
    /// geofence/survey polygons, rally points).
    /// Equivalent to QGC MapFitFunctions.fitMapViewportToAllItems().
    /// </summary>
    public static (MapGeoPoint Center, double ZoomLevel)? FitToAllItems(
        IEnumerable<MapGeoPoint>?        waypoints   = null,
        IEnumerable<MapCircleOverlay>?   circles     = null,
        IEnumerable<MapPolygonOverlay>?  polygons    = null,
        IEnumerable<RallyPointMarker>?   rallyPoints = null,
        double controlWidth  = 800,
        double controlHeight = 600)
    {
        var all = new List<MapGeoPoint>();

        if (waypoints is not null)
            all.AddRange(waypoints);

        if (circles is not null)
        {
            // Approximate circle with 4 cardinal edge points (r in degrees ≈ r_m / 111320)
            foreach (var c in circles)
            {
                var r = c.RadiusMeters / 111320.0;
                all.Add(new MapGeoPoint(c.Center.Latitude + r, c.Center.Longitude));
                all.Add(new MapGeoPoint(c.Center.Latitude - r, c.Center.Longitude));
                all.Add(new MapGeoPoint(c.Center.Latitude, c.Center.Longitude + r));
                all.Add(new MapGeoPoint(c.Center.Latitude, c.Center.Longitude - r));
            }
        }

        if (polygons is not null)
            foreach (var p in polygons)
                all.AddRange(p.Vertices);

        if (rallyPoints is not null)
            all.AddRange(rallyPoints.Select(rp => rp.Position));

        return FitToCoordinates(all, controlWidth, controlHeight);
    }
}

// ════════════════════════════════════════════════════════════════
// Obstacle-distance / proximity-radar controls (#103-107)
// QGC equivalents: ObstacleDistanceOverlay.qml,
//                  ObstacleDistanceOverlayVideo.qml,
//                  ProximityRadarMapView.qml (FlightMap),
//                  OnScreenGimbalController.qml,
//                  OnScreenCameraTrackingController.qml
// ════════════════════════════════════════════════════════════════

// ────────────────────────────────────────────────────────────────
// 103. ObstacleDistanceData  (data model — no visual)
//      QGC: FlyView/ObstacleDistanceOverlay.qml
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Obstacle-avoidance distance snapshot — the data model shared by both
/// <see cref="ProximityRadarVideoView"/> and <see cref="ProximityRadarMapView"/>.
/// Mirrors QGC's <c>objectAvoidance</c> vehicle property bag.
/// </summary>
public sealed class ObstacleDistanceData
{
    /// <summary>
    /// Distance readings in centimetres, one per bearing sector.
    /// Count must equal <c>360 / IncrementDegrees</c>.
    /// </summary>
    public IReadOnlyList<double> DistancesCm { get; init; } = Array.Empty<double>();

    /// <summary>Degrees between consecutive readings (e.g. 10 for 36 readings per rev).</summary>
    public double IncrementDegrees { get; init; } = 0;

    /// <summary>
    /// Angular offset applied before indexing into <see cref="DistancesCm"/>.
    /// Compensates for sensor mounting direction relative to vehicle front.
    /// </summary>
    public double AngleOffsetDegrees { get; init; } = 0;

    /// <summary>Maximum sensor range in centimetres.</summary>
    public double MaxDistanceCm { get; init; } = 1200;

    /// <summary>True when readings are available and the increment is non-zero.</summary>
    public bool IsAvailable => DistancesCm.Count > 0 && IncrementDegrees > 0;

    /// <summary>
    /// Returns the obstacle distance in metres for the given compass
    /// <paramref name="bearingDeg"/> (0 = North, CW), optionally subtracting the
    /// current vehicle <paramref name="headingDeg"/> to get a body-frame bearing.
    /// Returns <see cref="MaxDistanceCm"/>/100 when data is unavailable.
    /// </summary>
    public double GetRangeMetersAt(double bearingDeg, double headingDeg = 0)
    {
        if (!IsAvailable) return MaxDistanceCm / 100.0;

        var adjusted = ((bearingDeg + AngleOffsetDegrees - headingDeg) % 360 + 360) % 360;
        var idx      = (int)(adjusted / IncrementDegrees) % DistancesCm.Count;
        return DistancesCm[idx] / 100.0;
    }
}

// ── Shared ring-segment renderer ─────────────────────────────────

/// <summary>Shared Canvas utilities used by both proximity radar views.</summary>
internal static class RadarPainter
{
    /// <summary>Returns a screen point at <paramref name="bearingDeg"/> (0=North, CW) on radius <paramref name="r"/>.</summary>
    internal static Point BearingPt(double cx, double cy, double r, double bearingDeg)
    {
        var rad = bearingDeg * Math.PI / 180.0;
        return new Point(cx + r * Math.Sin(rad), cy - r * Math.Cos(rad));
    }

    /// <summary>
    /// Fills an annular sector (donut slice) from <paramref name="rInner"/> to
    /// <paramref name="rOuter"/> between compass bearings <paramref name="bearingA"/> and
    /// <paramref name="bearingB"/> (both CW from North).
    /// </summary>
    internal static void DrawRingSegment(DrawingContext ctx, double cx, double cy,
        double rInner, double rOuter, double bearingA, double bearingB, IBrush fill)
    {
        var span   = bearingB - bearingA;
        var large  = Math.Abs(span) > 180;

        var p1 = BearingPt(cx, cy, rInner, bearingA);
        var p2 = BearingPt(cx, cy, rOuter, bearingA);
        var p3 = BearingPt(cx, cy, rOuter, bearingB);
        var p4 = BearingPt(cx, cy, rInner, bearingB);

        var geo = new StreamGeometry();
        using (var gctx = geo.Open())
        {
            gctx.BeginFigure(p1, true);
            gctx.LineTo(p2);
            gctx.ArcTo(p3, new Size(rOuter, rOuter), 0, large, SweepDirection.Clockwise);
            gctx.LineTo(p4);
            gctx.ArcTo(p1, new Size(rInner, rInner), 0, large, SweepDirection.CounterClockwise);
            gctx.EndFigure(true);
        }
        ctx.DrawGeometry(fill, null, geo);
    }

    /// <summary>
    /// Returns a semi-transparent fill brush for a segment based on <paramref name="rangeM"/>
    /// relative to <paramref name="maxRangeM"/>.
    /// Red = close, orange = medium, green = far, grey = no data.
    /// </summary>
    internal static IBrush SegmentBrush(double rangeM, double maxRangeM)
    {
        const byte alpha = 180;
        if (maxRangeM <= 0) return new SolidColorBrush(Color.FromArgb(alpha, 128, 128, 128));
        var ratio = Math.Clamp(rangeM / maxRangeM, 0, 1);
        if (ratio < 0.20) return new SolidColorBrush(Color.FromArgb(alpha, 220, 40,  40));   // red
        if (ratio < 0.50) return new SolidColorBrush(Color.FromArgb(alpha, 220, 130,  0));   // orange
        return              new SolidColorBrush(Color.FromArgb(alpha,  40, 190,  40));         // green
    }
}

// ────────────────────────────────────────────────────────────────
// 105. ProximityRadarVideoView
//      QGC: FlyView/ObstacleDistanceOverlayVideo.qml
//      16-segment polar radar overlay rendered on top of video.
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Full-area polar radar overlay for the video stream.
/// Renders 16 equal bearing sectors as filled ring segments coloured by
/// obstacle distance (red = near, orange = mid, green = far).
/// </summary>
public sealed class ProximityRadarVideoView : Control
{
    private const int   Segments   = 16;
    private const double SegDeg    = 360.0 / Segments;
    private const double MinRatio  = 0.2;  // inner radius as fraction of outer

    public static readonly StyledProperty<ObstacleDistanceData?> ObstacleDataProperty =
        AvaloniaProperty.Register<ProximityRadarVideoView, ObstacleDistanceData?>(
            nameof(ObstacleData));

    public static readonly StyledProperty<double> VehicleHeadingProperty =
        AvaloniaProperty.Register<ProximityRadarVideoView, double>(nameof(VehicleHeading), 0);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<ProximityRadarVideoView, bool>(nameof(ShowLabels), true);

    static ProximityRadarVideoView() =>
        AffectsRender<ProximityRadarVideoView>(
            ObstacleDataProperty, VehicleHeadingProperty, ShowLabelsProperty);

    public ObstacleDistanceData? ObstacleData
    {
        get => GetValue(ObstacleDataProperty);
        set => SetValue(ObstacleDataProperty, value);
    }

    public double VehicleHeading
    {
        get => GetValue(VehicleHeadingProperty);
        set => SetValue(VehicleHeadingProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var data = ObstacleData;
        if (data is null || !data.IsAvailable) return;

        var w       = Bounds.Width;
        var h       = Bounds.Height;
        var cx      = w / 2;
        var cy      = h / 2;
        var rOuter  = Math.Min(w, h) * 0.45;
        var rInner  = rOuter * MinRatio;
        var maxM    = data.MaxDistanceCm / 100.0;

        for (var i = 0; i < Segments; i++)
        {
            var bA     = i * SegDeg;
            var bB     = bA + SegDeg - 0.5;  // slight gap between segments
            var range  = data.GetRangeMetersAt(bA + SegDeg / 2, VehicleHeading);
            var fill   = RadarPainter.SegmentBrush(range, maxM);
            RadarPainter.DrawRingSegment(context, cx, cy, rInner, rOuter, bA, bB, fill);
        }

        // Center label showing max range
        if (ShowLabels)
        {
            var tf   = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
            var text = new FormattedText($"Max: {maxM:F0} m", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, ScreenMetrics.SmallFontPointSize,
                new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)));
            context.DrawText(text, new Point(cx - text.Width / 2, cy - text.Height / 2));
        }
    }
}

// ────────────────────────────────────────────────────────────────
// 104. ProximityRadarMapView
//      QGC: FlightMap/MapItems/ProximityRadarMapView.qml
//      Same 16-segment radar but centred on the vehicle screen position.
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Polar radar overlay rendered on the map, centred on the vehicle's screen position.
/// Uses the same segment scheme as <see cref="ProximityRadarVideoView"/>;
/// the caller is responsible for converting the vehicle's GPS coordinate to
/// <see cref="VehicleScreenPosition"/> using the map's coordinate transform.
/// </summary>
public sealed class ProximityRadarMapView : Control
{
    private const int    Segments = 16;
    private const double SegDeg   = 360.0 / Segments;

    public static readonly StyledProperty<ObstacleDistanceData?> ObstacleDataProperty =
        AvaloniaProperty.Register<ProximityRadarMapView, ObstacleDistanceData?>(
            nameof(ObstacleData));

    public static readonly StyledProperty<double> VehicleHeadingProperty =
        AvaloniaProperty.Register<ProximityRadarMapView, double>(nameof(VehicleHeading), 0);

    /// <summary>Vehicle position in the control's local coordinate space (pixels).</summary>
    public static readonly StyledProperty<Point> VehicleScreenPositionProperty =
        AvaloniaProperty.Register<ProximityRadarMapView, Point>(
            nameof(VehicleScreenPosition), new Point(-1, -1));

    /// <summary>Outer radar radius in device pixels.</summary>
    public static readonly StyledProperty<double> RadarRadiusProperty =
        AvaloniaProperty.Register<ProximityRadarMapView, double>(nameof(RadarRadius), 80);

    static ProximityRadarMapView() =>
        AffectsRender<ProximityRadarMapView>(ObstacleDataProperty, VehicleHeadingProperty,
            VehicleScreenPositionProperty, RadarRadiusProperty);

    public ObstacleDistanceData? ObstacleData
    {
        get => GetValue(ObstacleDataProperty);
        set => SetValue(ObstacleDataProperty, value);
    }

    public double VehicleHeading
    {
        get => GetValue(VehicleHeadingProperty);
        set => SetValue(VehicleHeadingProperty, value);
    }

    public Point VehicleScreenPosition
    {
        get => GetValue(VehicleScreenPositionProperty);
        set => SetValue(VehicleScreenPositionProperty, value);
    }

    public double RadarRadius
    {
        get => GetValue(RadarRadiusProperty);
        set => SetValue(RadarRadiusProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var data = ObstacleData;
        var pos  = VehicleScreenPosition;
        if (data is null || !data.IsAvailable || pos.X < 0) return;

        var rOuter = RadarRadius;
        var rInner = rOuter * 0.2;
        var maxM   = data.MaxDistanceCm / 100.0;

        for (var i = 0; i < Segments; i++)
        {
            var bA    = i * SegDeg;
            var bB    = bA + SegDeg - 0.5;
            var range = data.GetRangeMetersAt(bA + SegDeg / 2, VehicleHeading);
            var fill  = RadarPainter.SegmentBrush(range, maxM);
            RadarPainter.DrawRingSegment(context, pos.X, pos.Y, rInner, rOuter, bA, bB, fill);
        }
    }
}

// ────────────────────────────────────────────────────────────────
// 106. OnScreenGimbalController
//      QGC: FlyView/OnScreenGimbalController.qml
//      Transparent touch overlay — click/drag → normalized gimbal commands.
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Full-area transparent overlay that converts pointer interactions into
/// normalized gimbal control commands.
/// <para>
/// In <em>click mode</em> (<see cref="ClickAndDragMode"/> = false):
/// each click fires <see cref="GimbalPointRequested"/> with the normalised
/// (−1…+1) position relative to the centre.
/// </para>
/// <para>
/// In <em>drag mode</em> (<see cref="ClickAndDragMode"/> = true):
/// a drag fires <see cref="GimbalDeltaRequested"/> continuously with the
/// (dx, dy) offset from the drag-start position; <see cref="GimbalDragEnded"/>
/// fires on pointer release.
/// </para>
/// </summary>
public sealed class OnScreenGimbalController : Control
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<OnScreenGimbalController, bool>(nameof(IsActive), false);

    public static readonly StyledProperty<bool> ClickAndDragModeProperty =
        AvaloniaProperty.Register<OnScreenGimbalController, bool>(nameof(ClickAndDragMode), false);

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool ClickAndDragMode
    {
        get => GetValue(ClickAndDragModeProperty);
        set => SetValue(ClickAndDragModeProperty, value);
    }

    // ── Events ──

    /// <summary>
    /// Fired in click mode when the user clicks the overlay.
    /// X and Y are normalised to −1…+1 relative to the control centre.
    /// </summary>
    public event EventHandler<(double X, double Y)>? GimbalPointRequested;

    /// <summary>
    /// Fired continuously in drag mode while the pointer is pressed.
    /// DX and DY are normalised deltas from the drag start position.
    /// </summary>
    public event EventHandler<(double Dx, double Dy)>? GimbalDeltaRequested;

    /// <summary>Fired when a drag ends (pointer released in drag mode).</summary>
    public event EventHandler? GimbalDragEnded;

    // ── State ──
    private bool  _dragging;
    private Point _dragStartNorm;
    private Point _dragStart;

    private (double X, double Y) Normalize(Point pt) =>
        ((pt.X / Bounds.Width) * 2 - 1,
         -((pt.Y / Bounds.Height) * 2 - 1));  // Y is inverted (up = positive)

    // Transparent — no rendering
    public override void Render(DrawingContext context) { }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsActive) return;

        var pt = e.GetPosition(this);
        if (ClickAndDragMode)
        {
            _dragging      = true;
            _dragStart     = pt;
            _dragStartNorm = new Point(Normalize(pt).X, Normalize(pt).Y);
            e.Pointer.Capture(this);
        }
        else
        {
            var (nx, ny) = Normalize(pt);
            GimbalPointRequested?.Invoke(this, (nx, ny));
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || !IsActive) return;

        var (nx, ny) = Normalize(e.GetPosition(this));
        var dx = nx - _dragStartNorm.X;
        var dy = ny - _dragStartNorm.Y;
        GimbalDeltaRequested?.Invoke(this, (dx, dy));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            GimbalDragEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}

// ────────────────────────────────────────────────────────────────
// 107. OnScreenCameraTrackingController
//      QGC: FlyView/OnScreenCameraTrackingController.qml
//      Transparent overlay for point/rect camera tracking with visual feedback.
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Full-area transparent overlay for camera tracking control.
/// Supports both point tracking (click) and rectangle tracking (drag).
/// Renders:
/// <list type="bullet">
///   <item>Green translucent rectangle while the user drags (ROI selection)</item>
///   <item>Red rectangle or circle showing the active tracking target (from camera feedback)</item>
/// </list>
/// <para>
/// All tracking coordinates are normalised to 0–1 relative to the video frame.
/// Set <see cref="VideoDisplayWidth"/> and <see cref="VideoDisplayHeight"/> for
/// correct letterbox-margin compensation.
/// </para>
/// </summary>
public sealed class OnScreenCameraTrackingController : Control
{
    // ── Input capabilities ──

    public static readonly StyledProperty<bool> CanTrackPointProperty =
        AvaloniaProperty.Register<OnScreenCameraTrackingController, bool>(nameof(CanTrackPoint), false);

    public static readonly StyledProperty<bool> CanTrackRectProperty =
        AvaloniaProperty.Register<OnScreenCameraTrackingController, bool>(nameof(CanTrackRect), false);

    // ── Tracking feedback (from camera, normalized 0-1) ──

    public static readonly StyledProperty<bool> TrackingActiveProperty =
        AvaloniaProperty.Register<OnScreenCameraTrackingController, bool>(nameof(TrackingActive), false);

    public static readonly StyledProperty<bool> TrackingIsPointProperty =
        AvaloniaProperty.Register<OnScreenCameraTrackingController, bool>(nameof(TrackingIsPoint), false);

    /// <summary>Normalised tracking target (X,Y = top-left, W,H = size in 0-1 units).</summary>
    public static readonly StyledProperty<Rect> TrackingRectNormProperty =
        AvaloniaProperty.Register<OnScreenCameraTrackingController, Rect>(
            nameof(TrackingRectNorm), default);

    /// <summary>Width of the displayed video frame in device pixels (for letterbox math).</summary>
    public static readonly StyledProperty<double> VideoDisplayWidthProperty =
        AvaloniaProperty.Register<OnScreenCameraTrackingController, double>(
            nameof(VideoDisplayWidth), 0);

    /// <summary>Height of the displayed video frame in device pixels (for letterbox math).</summary>
    public static readonly StyledProperty<double> VideoDisplayHeightProperty =
        AvaloniaProperty.Register<OnScreenCameraTrackingController, double>(
            nameof(VideoDisplayHeight), 0);

    static OnScreenCameraTrackingController() =>
        AffectsRender<OnScreenCameraTrackingController>(
            TrackingActiveProperty, TrackingIsPointProperty, TrackingRectNormProperty);

    public bool CanTrackPoint     { get => GetValue(CanTrackPointProperty);    set => SetValue(CanTrackPointProperty, value); }
    public bool CanTrackRect      { get => GetValue(CanTrackRectProperty);     set => SetValue(CanTrackRectProperty, value); }
    public bool TrackingActive    { get => GetValue(TrackingActiveProperty);   set => SetValue(TrackingActiveProperty, value); }
    public bool TrackingIsPoint   { get => GetValue(TrackingIsPointProperty);  set => SetValue(TrackingIsPointProperty, value); }
    public Rect TrackingRectNorm  { get => GetValue(TrackingRectNormProperty); set => SetValue(TrackingRectNormProperty, value); }
    public double VideoDisplayWidth  { get => GetValue(VideoDisplayWidthProperty);  set => SetValue(VideoDisplayWidthProperty, value); }
    public double VideoDisplayHeight { get => GetValue(VideoDisplayHeightProperty); set => SetValue(VideoDisplayHeightProperty, value); }

    // ── Events ──

    /// <summary>Fired when the user clicks for point-tracking. NX/NY are normalized 0-1 video coordinates.</summary>
    public event EventHandler<(double NX, double NY, double NRadius)>? TrackPointRequested;

    /// <summary>Fired when the user finishes dragging a rect-tracking ROI. Rect is normalized 0-1 video coordinates.</summary>
    public event EventHandler<Rect>? TrackRectRequested;

    // ── Drag state ──
    private bool  _dragging;
    private Point _dragStart;
    private Point _dragCurrent;

    private (double MarginH, double MarginV) Margins()
    {
        var vw = VideoDisplayWidth;
        var vh = VideoDisplayHeight;
        return vw > 0 && vh > 0
            ? ((Bounds.Width  - vw) / 2, (Bounds.Height - vh) / 2)
            : (0, 0);
    }

    private Rect ScreenRectToNorm(double x0, double y0, double x1, double y1)
    {
        var (mh, mv) = Margins();
        var vw = VideoDisplayWidth  > 0 ? VideoDisplayWidth  : Bounds.Width;
        var vh = VideoDisplayHeight > 0 ? VideoDisplayHeight : Bounds.Height;
        double Clamp01(double v) => Math.Clamp(v, 0, 1);
        var nx0 = Clamp01((x0 - mh) / vw);
        var ny0 = Clamp01((y0 - mv) / vh);
        var nx1 = Clamp01((x1 - mh) / vw);
        var ny1 = Clamp01((y1 - mv) / vh);
        return new Rect(nx0, ny0, nx1 - nx0, ny1 - ny0);
    }

    private Rect NormRectToScreen(Rect norm)
    {
        var (mh, mv) = Margins();
        var vw = VideoDisplayWidth  > 0 ? VideoDisplayWidth  : Bounds.Width;
        var vh = VideoDisplayHeight > 0 ? VideoDisplayHeight : Bounds.Height;
        return new Rect(
            mh + norm.X      * vw,
            mv + norm.Y      * vh,
                 norm.Width  * vw,
                 norm.Height * vh);
    }

    // ── Render ──

    public override void Render(DrawingContext context)
    {
        // Green ROI rect while dragging
        if (_dragging)
        {
            var rx = Math.Min(_dragStart.X, _dragCurrent.X);
            var ry = Math.Min(_dragStart.Y, _dragCurrent.Y);
            var rw = Math.Abs(_dragCurrent.X - _dragStart.X);
            var rh = Math.Abs(_dragCurrent.Y - _dragStart.Y);
            if (rw > 4 && rh > 4)
            {
                var fill = new SolidColorBrush(Color.FromArgb(64, 26, 220, 26));
                var pen  = new Pen(new SolidColorBrush(Colors.Lime), 1);
                context.DrawRectangle(fill, pen, new Rect(rx, ry, rw, rh));
            }
        }

        // Red tracking-status feedback
        if (TrackingActive)
        {
            var sr  = NormRectToScreen(TrackingRectNorm);
            var pen = new Pen(new SolidColorBrush(Colors.Red), 4);
            if (TrackingIsPoint)
                context.DrawEllipse(null, pen, sr.Center, sr.Width / 2, sr.Height / 2);
            else
                context.DrawRectangle(null, pen, sr, 4, 4);
        }
    }

    // ── Pointer handling ──

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetPosition(this);

        if (CanTrackRect)
        {
            _dragging    = true;
            _dragStart   = pt;
            _dragCurrent = pt;
            e.Pointer.Capture(this);
        }
        else if (CanTrackPoint)
        {
            var (mh, mv) = Margins();
            var vw = VideoDisplayWidth  > 0 ? VideoDisplayWidth  : Bounds.Width;
            var vh = VideoDisplayHeight > 0 ? VideoDisplayHeight : Bounds.Height;
            var nx = Math.Clamp((pt.X - mh) / vw, 0, 1);
            var ny = Math.Clamp((pt.Y - mv) / vh, 0, 1);
            const double pointRadius = 20;
            TrackPointRequested?.Invoke(this,
                (nx, ny, Math.Min(pointRadius / Math.Max(vw, 1), 1.0)));
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        _dragCurrent = e.GetPosition(this);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;

        var pt = e.GetPosition(this);
        var x0 = Math.Min(_dragStart.X, pt.X);
        var y0 = Math.Min(_dragStart.Y, pt.Y);
        var x1 = Math.Max(_dragStart.X, pt.X);
        var y1 = Math.Max(_dragStart.Y, pt.Y);

        var norm = ScreenRectToNorm(x0, y0, x1, y1);
        if (norm.Width >= 0.01 && norm.Height >= 0.01)
            TrackRectRequested?.Invoke(this, norm);

        InvalidateVisual();
    }
}

// ═══════════════════════════════════════════════════════════════
// PhotoVideoControl (#108)
// Equivalent to QGC FlightMap/Widgets/PhotoVideoControl.qml
// Semi-transparent overlay: photo/video mode pill, capture buttons,
// optional zoom slider, tracking toggle, and status row.
// ═══════════════════════════════════════════════════════════════

public sealed class PhotoVideoControl : Control
{
    private const double Pad      = 8;
    private const double PillW    = 96;
    private const double PillH    = 28;
    private const double CaptureR = 24;
    private const double ZoomColW = 28;
    private const double ZoomColH = 120;
    private const double TrackR   = 20;

    // ── Properties ──────────────────────────────────────────────

    public static readonly StyledProperty<bool> HasVideoModeProperty =
        AvaloniaProperty.Register<PhotoVideoControl, bool>(nameof(HasVideoMode));
    public static readonly StyledProperty<bool> HasPhotoModeProperty =
        AvaloniaProperty.Register<PhotoVideoControl, bool>(nameof(HasPhotoMode), true);
    public static readonly StyledProperty<bool> IsVideoModeProperty =
        AvaloniaProperty.Register<PhotoVideoControl, bool>(nameof(IsVideoMode), true);
    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<PhotoVideoControl, bool>(nameof(IsRecording));
    public static readonly StyledProperty<bool> IsTakingPhotoProperty =
        AvaloniaProperty.Register<PhotoVideoControl, bool>(nameof(IsTakingPhoto));
    public static readonly StyledProperty<string> RecordTimeStrProperty =
        AvaloniaProperty.Register<PhotoVideoControl, string>(nameof(RecordTimeStr), "00:00:00");
    public static readonly StyledProperty<int> PhotoCaptureCountProperty =
        AvaloniaProperty.Register<PhotoVideoControl, int>(nameof(PhotoCaptureCount));
    public static readonly StyledProperty<string> StorageFreeStrProperty =
        AvaloniaProperty.Register<PhotoVideoControl, string>(nameof(StorageFreeStr), "");
    public static readonly StyledProperty<int> CameraBatteryPercentProperty =
        AvaloniaProperty.Register<PhotoVideoControl, int>(nameof(CameraBatteryPercent), -1);
    public static readonly StyledProperty<bool> HasCameraTrackingProperty =
        AvaloniaProperty.Register<PhotoVideoControl, bool>(nameof(HasCameraTracking));
    public static readonly StyledProperty<bool> CameraTrackingEnabledProperty =
        AvaloniaProperty.Register<PhotoVideoControl, bool>(nameof(CameraTrackingEnabled));
    public static readonly StyledProperty<bool> HasCameraZoomProperty =
        AvaloniaProperty.Register<PhotoVideoControl, bool>(nameof(HasCameraZoom));
    public static readonly StyledProperty<double> CameraZoomLevelProperty =
        AvaloniaProperty.Register<PhotoVideoControl, double>(nameof(CameraZoomLevel), 50.0);

    static PhotoVideoControl()
    {
        AffectsRender<PhotoVideoControl>(
            HasVideoModeProperty, HasPhotoModeProperty, IsVideoModeProperty,
            IsRecordingProperty, IsTakingPhotoProperty, RecordTimeStrProperty,
            PhotoCaptureCountProperty, StorageFreeStrProperty, CameraBatteryPercentProperty,
            HasCameraTrackingProperty, CameraTrackingEnabledProperty,
            HasCameraZoomProperty, CameraZoomLevelProperty);
        AffectsMeasure<PhotoVideoControl>(
            HasVideoModeProperty, HasPhotoModeProperty,
            HasCameraTrackingProperty, HasCameraZoomProperty);
    }

    public bool   HasVideoMode          { get => GetValue(HasVideoModeProperty);          set => SetValue(HasVideoModeProperty, value); }
    public bool   HasPhotoMode          { get => GetValue(HasPhotoModeProperty);          set => SetValue(HasPhotoModeProperty, value); }
    public bool   IsVideoMode           { get => GetValue(IsVideoModeProperty);           set => SetValue(IsVideoModeProperty, value); }
    public bool   IsRecording           { get => GetValue(IsRecordingProperty);           set => SetValue(IsRecordingProperty, value); }
    public bool   IsTakingPhoto         { get => GetValue(IsTakingPhotoProperty);         set => SetValue(IsTakingPhotoProperty, value); }
    public string RecordTimeStr         { get => GetValue(RecordTimeStrProperty);         set => SetValue(RecordTimeStrProperty, value); }
    public int    PhotoCaptureCount     { get => GetValue(PhotoCaptureCountProperty);     set => SetValue(PhotoCaptureCountProperty, value); }
    public string StorageFreeStr        { get => GetValue(StorageFreeStrProperty);        set => SetValue(StorageFreeStrProperty, value); }
    public int    CameraBatteryPercent  { get => GetValue(CameraBatteryPercentProperty);  set => SetValue(CameraBatteryPercentProperty, value); }
    public bool   HasCameraTracking     { get => GetValue(HasCameraTrackingProperty);     set => SetValue(HasCameraTrackingProperty, value); }
    public bool   CameraTrackingEnabled { get => GetValue(CameraTrackingEnabledProperty); set => SetValue(CameraTrackingEnabledProperty, value); }
    public bool   HasCameraZoom         { get => GetValue(HasCameraZoomProperty);         set => SetValue(HasCameraZoomProperty, value); }
    public double CameraZoomLevel       { get => GetValue(CameraZoomLevelProperty);       set => SetValue(CameraZoomLevelProperty, value); }

    // ── Events ──────────────────────────────────────────────────
    public event EventHandler?         SetVideoModeRequested;
    public event EventHandler?         SetPhotoModeRequested;
    public event EventHandler?         ToggleVideoRecordingRequested;
    public event EventHandler?         TakePhotoRequested;
    public event EventHandler?         StopPhotoRequested;
    public event EventHandler?         TrackingToggleRequested;
    public event EventHandler<double>? ZoomLevelChanged;

    // ── Internal hit-test areas (set during Render) ──────────────
    private Rect _videoPillArea;
    private Rect _photoPillArea;
    private Rect _videoCaptureArea;
    private Rect _photoCaptureArea;
    private Rect _trackingArea;
    private Rect _zoomTrackRect;
    private bool _zoomDragging;

    // ── Derived visibility ───────────────────────────────────────
    private bool ShowModePill     => HasVideoMode && HasPhotoMode;
    private bool ShowVideoCapture => ShowModePill ? IsVideoMode  : HasVideoMode;
    private bool ShowPhotoCapture => ShowModePill ? !IsVideoMode : HasPhotoMode;

    // ── Render ──────────────────────────────────────────────────
    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 10 || h < 10) return;

        var tf      = new Typeface("Segoe UI");
        var smallSz = ScreenMetrics.SmallFontPointSize;

        // Semi-transparent dark background
        var bgColor = Color.FromArgb(178, QgcColors.Window.R, QgcColors.Window.G, QgcColors.Window.B);
        ctx.DrawRectangle(new SolidColorBrush(bgColor), null, new Rect(0, 0, w, h), 8, 8);

        double innerX = Pad;

        // ── Zoom slider (left column) ──────────────────────────
        if (HasCameraZoom)
        {
            _zoomTrackRect = new Rect(Pad, Pad, ZoomColW, ZoomColH);
            RenderZoomSlider(ctx, tf, smallSz);
            innerX = Pad + ZoomColW + Pad;
        }

        var usableW = w - innerX - Pad;
        double y    = Pad;

        // ── Mode pill ──────────────────────────────────────────
        if (ShowModePill)
        {
            double px = innerX + (usableW - PillW) / 2;
            ctx.DrawRectangle(new SolidColorBrush(QgcColors.WindowShadeLight), null,
                new Rect(px, y, PillW, PillH), PillH / 2, PillH / 2);

            // Video side (left)
            _videoPillArea = new Rect(px, y, PillW / 2, PillH);
            if (IsVideoMode)
                ctx.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
                    new Rect(px, y, PillH, PillH), PillH / 2, PillH / 2);
            var vColor = new SolidColorBrush(IsVideoMode ? QgcColors.ColorGreen : QgcColors.TextSecondary);
            var vFt = new FormattedText("▶", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, PillH * 0.45, vColor);
            ctx.DrawText(vFt, new Point(px + (PillH - vFt.Width) / 2, y + (PillH - vFt.Height) / 2));

            // Photo side (right)
            _photoPillArea = new Rect(px + PillW / 2, y, PillW / 2, PillH);
            if (!IsVideoMode)
                ctx.DrawRectangle(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
                    new Rect(px + PillW - PillH, y, PillH, PillH), PillH / 2, PillH / 2);
            var pColor = new SolidColorBrush(!IsVideoMode ? QgcColors.ColorGreen : QgcColors.TextSecondary);
            var pFt = new FormattedText("◉", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, PillH * 0.45, pColor);
            ctx.DrawText(pFt, new Point(px + PillW - PillH + (PillH - pFt.Width) / 2, y + (PillH - pFt.Height) / 2));

            y += PillH + Pad;
        }

        // ── Capture buttons ────────────────────────────────────
        double btnCy  = y + CaptureR;
        bool showBoth = ShowVideoCapture && ShowPhotoCapture;

        if (ShowVideoCapture)
        {
            double cx = showBoth ? innerX + usableW / 4 : innerX + usableW / 2;
            RenderCaptureButton(ctx, cx, btnCy, isVideo: true);
            _videoCaptureArea = new Rect(cx - CaptureR, btnCy - CaptureR, CaptureR * 2, CaptureR * 2);
        }

        if (ShowPhotoCapture)
        {
            double cx = showBoth ? innerX + usableW * 3 / 4 : innerX + usableW / 2;
            RenderCaptureButton(ctx, cx, btnCy, isVideo: false);
            _photoCaptureArea = new Rect(cx - CaptureR, btnCy - CaptureR, CaptureR * 2, CaptureR * 2);
        }

        y = btnCy + CaptureR + Pad;

        // Labels when both buttons are visible
        if (showBoth)
        {
            var vLbl = new FormattedText("Video", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, smallSz, new SolidColorBrush(QgcColors.TextSecondary));
            ctx.DrawText(vLbl, new Point(innerX + usableW / 4 - vLbl.Width / 2, y));

            var pLbl = new FormattedText("Photo", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, smallSz, new SolidColorBrush(QgcColors.TextSecondary));
            ctx.DrawText(pLbl, new Point(innerX + usableW * 3 / 4 - pLbl.Width / 2, y));
            y += vLbl.Height + 4;
        }

        // ── Status lines ───────────────────────────────────────
        if (ShowVideoCapture)
        {
            var timeColor = new SolidColorBrush(IsRecording ? QgcColors.ColorRed : QgcColors.TextSecondary);
            var timeFt = new FormattedText(RecordTimeStr, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, smallSz, timeColor);
            double tx = innerX + (usableW - timeFt.Width) / 2;
            if (IsRecording)
            {
                var rbg = Color.FromArgb(80, QgcColors.ColorRed.R, QgcColors.ColorRed.G, QgcColors.ColorRed.B);
                ctx.DrawRectangle(new SolidColorBrush(rbg), null,
                    new Rect(tx - 4, y - 2, timeFt.Width + 8, timeFt.Height + 4), 3, 3);
            }
            ctx.DrawText(timeFt, new Point(tx, y));
            y += timeFt.Height + 2;
        }

        if (ShowPhotoCapture)
        {
            var cColor = new SolidColorBrush(IsTakingPhoto ? QgcColors.ColorGreen : QgcColors.TextSecondary);
            var cFt = new FormattedText(PhotoCaptureCount.ToString("D5"), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, smallSz, cColor);
            double cx2 = innerX + (usableW - cFt.Width) / 2;
            if (IsTakingPhoto)
            {
                var gbg = Color.FromArgb(80, QgcColors.ColorGreen.R, QgcColors.ColorGreen.G, QgcColors.ColorGreen.B);
                ctx.DrawRectangle(new SolidColorBrush(gbg), null,
                    new Rect(cx2 - 4, y - 2, cFt.Width + 8, cFt.Height + 4), 3, 3);
            }
            ctx.DrawText(cFt, new Point(cx2, y));
            y += cFt.Height + 2;
        }

        if (!string.IsNullOrEmpty(StorageFreeStr))
        {
            var sFt = new FormattedText($"Free: {StorageFreeStr}", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, smallSz, new SolidColorBrush(QgcColors.TextSecondary));
            ctx.DrawText(sFt, new Point(innerX + (usableW - sFt.Width) / 2, y));
            y += sFt.Height + 2;
        }

        if (CameraBatteryPercent >= 0)
        {
            var batColor = CameraBatteryPercent < 20 ? QgcColors.ColorRed : QgcColors.TextSecondary;
            var bFt = new FormattedText($"Battery: {CameraBatteryPercent}%", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, smallSz, new SolidColorBrush(batColor));
            ctx.DrawText(bFt, new Point(innerX + (usableW - bFt.Width) / 2, y));
            y += bFt.Height + 2;
        }

        // ── Tracking button ────────────────────────────────────
        if (HasCameraTracking)
        {
            y += 4;
            double trkCx = innerX + usableW / 2;
            double trkCy = y + TrackR;
            var trkFill = new SolidColorBrush(CameraTrackingEnabled ? QgcColors.ColorRed : QgcColors.WindowShadeLight);
            var trkPen  = new Pen(new SolidColorBrush(QgcColors.ButtonText), 2);
            ctx.DrawEllipse(trkFill, trkPen, new Point(trkCx, trkCy), TrackR, TrackR);
            // Crosshair icon
            var cp  = new Pen(new SolidColorBrush(QgcColors.ButtonText), 1.5);
            double hr = TrackR * 0.5;
            ctx.DrawLine(cp, new Point(trkCx - hr, trkCy), new Point(trkCx + hr, trkCy));
            ctx.DrawLine(cp, new Point(trkCx, trkCy - hr), new Point(trkCx, trkCy + hr));
            ctx.DrawEllipse(null, cp, new Point(trkCx, trkCy), hr * 0.5, hr * 0.5);
            _trackingArea = new Rect(trkCx - TrackR, y, TrackR * 2, TrackR * 2);

            y += TrackR * 2 + 2;
            var tLbl = new FormattedText("Tracking", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, smallSz, new SolidColorBrush(QgcColors.TextSecondary));
            ctx.DrawText(tLbl, new Point(trkCx - tLbl.Width / 2, y));
        }
    }

    private void RenderZoomSlider(DrawingContext ctx, Typeface tf, double smallSz)
    {
        double cx    = Pad + ZoomColW / 2;
        double top   = _zoomTrackRect.Top;
        double trackH = _zoomTrackRect.Height;

        // Track
        ctx.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade), null,
            new Rect(cx - 3, top, 6, trackH), 3, 3);
        // Thumb
        double thumbY = top + trackH * (1.0 - Math.Clamp(CameraZoomLevel / 100.0, 0, 1));
        ctx.DrawEllipse(new SolidColorBrush(QgcColors.PrimaryButtonFill), null,
            new Point(cx, thumbY), 8, 8);
        // Label
        var zLbl = new FormattedText("Zoom", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, tf, smallSz, new SolidColorBrush(QgcColors.TextSecondary));
        ctx.DrawText(zLbl, new Point(cx - zLbl.Width / 2, top + trackH + 2));
    }

    private void RenderCaptureButton(DrawingContext ctx, double cx, double cy, bool isVideo)
    {
        bool isActive = isVideo ? IsRecording : IsTakingPhoto;
        var border    = new Pen(new SolidColorBrush(QgcColors.ButtonText), 1.5);

        // Outer circle
        ctx.DrawEllipse(new SolidColorBrush(QgcColors.WindowShade), border, new Point(cx, cy), CaptureR, CaptureR);
        // Mid ring (75%)
        ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), null,
            new Point(cx, cy), CaptureR * 0.75, CaptureR * 0.75);

        // Inner indicator
        double innerR    = CaptureR * (isActive ? 0.38 : 0.60);
        var indicatorCol = isVideo
            ? (IsRecording    ? QgcColors.ColorRed   : Colors.White)
            : (IsTakingPhoto  ? QgcColors.ColorGreen : Colors.White);

        if (isVideo && IsRecording)
        {
            // Stop: rounded square
            double sq = innerR * 1.4;
            ctx.DrawRectangle(new SolidColorBrush(indicatorCol), null,
                new Rect(cx - sq / 2, cy - sq / 2, sq, sq), 3, 3);
        }
        else
        {
            ctx.DrawEllipse(new SolidColorBrush(indicatorCol), null, new Point(cx, cy), innerR, innerR);
        }
    }

    // ── Input ───────────────────────────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetPosition(this);

        if (ShowModePill)
        {
            if (_videoPillArea.Contains(pt)) { SetVideoModeRequested?.Invoke(this, EventArgs.Empty);  e.Handled = true; return; }
            if (_photoPillArea.Contains(pt)) { SetPhotoModeRequested?.Invoke(this, EventArgs.Empty);  e.Handled = true; return; }
        }
        if (ShowVideoCapture && _videoCaptureArea.Contains(pt))
        {
            ToggleVideoRecordingRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true; return;
        }
        if (ShowPhotoCapture && _photoCaptureArea.Contains(pt))
        {
            if (IsTakingPhoto) StopPhotoRequested?.Invoke(this, EventArgs.Empty);
            else               TakePhotoRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true; return;
        }
        if (HasCameraTracking && _trackingArea.Contains(pt))
        {
            TrackingToggleRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true; return;
        }
        if (HasCameraZoom && _zoomTrackRect.Contains(pt))
        {
            UpdateZoomFromPointer(pt);
            e.Pointer.Capture(this);
            _zoomDragging = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_zoomDragging) UpdateZoomFromPointer(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_zoomDragging) return;
        _zoomDragging = false;
        e.Pointer.Capture(null);
    }

    private void UpdateZoomFromPointer(Point pt)
    {
        if (_zoomTrackRect.Height <= 0) return;
        var norm = 1.0 - Math.Clamp((pt.Y - _zoomTrackRect.Top) / _zoomTrackRect.Height, 0, 1);
        CameraZoomLevel = norm * 100.0;
        ZoomLevelChanged?.Invoke(this, CameraZoomLevel);
        InvalidateVisual();
    }

    // ── Layout ──────────────────────────────────────────────────
    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
        double h = Pad;
        if (ShowModePill)                             h += PillH + Pad;
        h += CaptureR * 2 + Pad;
        if (ShowVideoCapture && ShowPhotoCapture)     h += 14 + 4;
        if (ShowVideoCapture)                         h += 14 + 2;
        if (ShowPhotoCapture)                         h += 14 + 2;
        if (!string.IsNullOrEmpty(StorageFreeStr))    h += 14 + 2;
        if (CameraBatteryPercent >= 0)                h += 14 + 2;
        if (HasCameraTracking)                        h += 4 + TrackR * 2 + 2 + 14;
        h += Pad;
        return new Size(w, h);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// VehicleStatusBar  (#152 — compact horizontal vehicle summary strip)
// Renders: colored FlightMode pill | ArmedText | BatteryPercent bar | GPS count
// Suitable for embedding in the FlyView bottom bar or PiP overlay.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class VehicleStatusBar : Control
{
    public static readonly StyledProperty<string> FlightModeProperty =
        AvaloniaProperty.Register<VehicleStatusBar, string>(nameof(FlightMode), "—");
    public static readonly StyledProperty<string> ArmedTextProperty =
        AvaloniaProperty.Register<VehicleStatusBar, string>(nameof(ArmedText), "DISARMED");
    public static readonly StyledProperty<bool>   IsArmedProperty =
        AvaloniaProperty.Register<VehicleStatusBar, bool>(nameof(IsArmed), false);
    public static readonly StyledProperty<double> BatteryPercentProperty =
        AvaloniaProperty.Register<VehicleStatusBar, double>(nameof(BatteryPercent), -1.0);
    public static readonly StyledProperty<int>    GpsSatelliteCountProperty =
        AvaloniaProperty.Register<VehicleStatusBar, int>(nameof(GpsSatelliteCount), 0);
    public static readonly StyledProperty<Color>  StatusColorProperty =
        AvaloniaProperty.Register<VehicleStatusBar, Color>(nameof(StatusColor), QgcColors.ColorGreen);

    static VehicleStatusBar()
    {
        AffectsRender<VehicleStatusBar>(FlightModeProperty, ArmedTextProperty, IsArmedProperty,
            BatteryPercentProperty, GpsSatelliteCountProperty, StatusColorProperty);
    }

    public string FlightMode       { get => GetValue(FlightModeProperty);       set => SetValue(FlightModeProperty, value); }
    public string ArmedText        { get => GetValue(ArmedTextProperty);        set => SetValue(ArmedTextProperty, value); }
    public bool   IsArmed          { get => GetValue(IsArmedProperty);          set => SetValue(IsArmedProperty, value); }
    public double BatteryPercent   { get => GetValue(BatteryPercentProperty);   set => SetValue(BatteryPercentProperty, value); }
    public int    GpsSatelliteCount{ get => GetValue(GpsSatelliteCountProperty);set => SetValue(GpsSatelliteCountProperty, value); }
    public Color  StatusColor      { get => GetValue(StatusColorProperty);      set => SetValue(StatusColorProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var dfw    = ScreenMetrics.DefaultFontPixelWidth;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var h      = Bounds.Height;
        var pad    = 4.0;
        double x   = pad;
        double cy  = h / 2.0;
        double textY = (h - dfh * 0.9) / 2.0;
        var textFs = dfh * 0.85;

        // Flight mode pill
        var modeFt = new FormattedText(FlightMode, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, textFs, new SolidColorBrush(Colors.White));
        double pillW = modeFt.Width + dfw;
        double pillH = dfh * 1.1;
        var pillRect = new Rect(x, (h - pillH) / 2, pillW, pillH);
        ctx.DrawRectangle(new SolidColorBrush(StatusColor), null, pillRect, 3, 3);
        ctx.DrawText(modeFt, new Point(x + dfw * 0.5, (h - modeFt.Height) / 2));
        x += pillW + pad;

        // Armed / Disarmed text
        var armedColor = IsArmed ? QgcColors.ColorRed : QgcColors.ColorGrey;
        var armedFt = new FormattedText(ArmedText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, textFs, new SolidColorBrush(armedColor));
        ctx.DrawText(armedFt, new Point(x, textY));
        x += armedFt.Width + pad * 2;

        // Battery bar (if available)
        if (BatteryPercent >= 0)
        {
            double barW  = dfw * 3;
            double barH  = dfh * 0.7;
            double barY  = (h - barH) / 2;
            var batColor = BatteryPercent > 50 ? QgcColors.ColorGreen :
                           BatteryPercent > 20 ? QgcColors.ColorOrange : QgcColors.ColorRed;
            ctx.DrawRectangle(new SolidColorBrush(QgcColors.Button), null, new Rect(x, barY, barW, barH));
            ctx.DrawRectangle(new SolidColorBrush(batColor), null,
                new Rect(x, barY, barW * Math.Clamp(BatteryPercent / 100.0, 0, 1), barH));
            var batFt = new FormattedText($"{(int)BatteryPercent}%",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, textFs * 0.85, new SolidColorBrush(QgcColors.Text));
            ctx.DrawText(batFt, new Point(x + barW + 2, textY));
            x += barW + 2 + batFt.Width + pad * 2;
        }

        // GPS satellite count
        if (GpsSatelliteCount >= 0)
        {
            var gpsFt = new FormattedText($"GPS:{GpsSatelliteCount}",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, textFs * 0.85,
                new SolidColorBrush(GpsSatelliteCount >= 6 ? QgcColors.ColorGreen :
                                    GpsSatelliteCount >= 3 ? QgcColors.ColorOrange : QgcColors.ColorRed));
            ctx.DrawText(gpsFt, new Point(x, textY));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = double.IsInfinity(availableSize.Width) ? ScreenMetrics.DefaultFontPixelWidth * 28 : availableSize.Width;
        return new Size(w, dfh * 1.6);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GeoFenceRectOverlay  (#159 — QGC QGCMapRectVisuals: rectangle geo-fence overlay)
// Draws a dashed rectangle in screen-space coordinates supplied by the map layer.
// IsInclusionZone: green border; exclusion: red border.  IsSelected adds corner handles.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GeoFenceRectOverlay : Control
{
    public static readonly StyledProperty<Point>  TopLeftPointProperty =
        AvaloniaProperty.Register<GeoFenceRectOverlay, Point>(nameof(TopLeftPoint), new Point(0, 0));
    public static readonly StyledProperty<Point>  BottomRightPointProperty =
        AvaloniaProperty.Register<GeoFenceRectOverlay, Point>(nameof(BottomRightPoint), new Point(100, 100));
    public static readonly StyledProperty<bool>   GeoIsInclusionZoneProperty =
        AvaloniaProperty.Register<GeoFenceRectOverlay, bool>(nameof(IsInclusionZone), true);
    public static readonly StyledProperty<bool>   GeoIsSelectedProperty =
        AvaloniaProperty.Register<GeoFenceRectOverlay, bool>(nameof(IsSelected), false);

    static GeoFenceRectOverlay()
    {
        AffectsRender<GeoFenceRectOverlay>(TopLeftPointProperty, BottomRightPointProperty,
            GeoIsInclusionZoneProperty, GeoIsSelectedProperty);
    }

    public Point TopLeftPoint     { get => GetValue(TopLeftPointProperty);     set => SetValue(TopLeftPointProperty, value); }
    public Point BottomRightPoint { get => GetValue(BottomRightPointProperty); set => SetValue(BottomRightPointProperty, value); }
    public bool  IsInclusionZone  { get => GetValue(GeoIsInclusionZoneProperty); set => SetValue(GeoIsInclusionZoneProperty, value); }
    public bool  IsSelected       { get => GetValue(GeoIsSelectedProperty);      set => SetValue(GeoIsSelectedProperty, value); }

    public event EventHandler? SelectionToggled;

    public override void Render(DrawingContext ctx)
    {
        var tl   = TopLeftPoint;
        var br   = BottomRightPoint;
        var rect = new Rect(tl, br);
        var borderColor = IsInclusionZone ? QgcColors.ColorGreen : QgcColors.ColorRed;

        // Fill (translucent)
        byte alpha = (byte)(IsSelected ? 40 : 20);
        ctx.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(alpha, borderColor.R, borderColor.G, borderColor.B)),
            null, rect);

        // Dashed border
        var pen = new Pen(new SolidColorBrush(borderColor), 1.5)
        {
            DashStyle = IsSelected ? DashStyle.Dash : DashStyle.DashDot
        };
        ctx.DrawRectangle(null, pen, rect);

        // Corner handles when selected
        if (IsSelected)
        {
            double hs = 6;
            foreach (var corner in new[] { tl, new Point(br.X, tl.Y), br, new Point(tl.X, br.Y) })
                ctx.DrawRectangle(new SolidColorBrush(Colors.White), new Pen(new SolidColorBrush(borderColor), 1),
                    new Rect(corner.X - hs / 2, corner.Y - hs / 2, hs, hs));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        SelectionToggled?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;
}

// ─────────────────────────────────────────────────────────────────────────────
// WaypointCountBadge  (#160 — circular badge showing total waypoint count)
// Used in the Plan view toolbar to indicate how many mission items exist.
// Count=0 renders transparent.  Background color can be customised.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class WaypointCountBadge : Control
{
    public static readonly StyledProperty<int>   BadgeCountProperty =
        AvaloniaProperty.Register<WaypointCountBadge, int>(nameof(BadgeCount), 0);
    public static readonly StyledProperty<Color> BadgeColorProperty =
        AvaloniaProperty.Register<WaypointCountBadge, Color>(nameof(BadgeColor), QgcColors.ColorBlue);
    public static readonly StyledProperty<double> BadgeSizeProperty =
        AvaloniaProperty.Register<WaypointCountBadge, double>(nameof(BadgeSize), 22.0);

    static WaypointCountBadge()
    {
        AffectsRender<WaypointCountBadge>(BadgeCountProperty, BadgeColorProperty, BadgeSizeProperty);
    }

    public int    BadgeCount { get => GetValue(BadgeCountProperty); set => SetValue(BadgeCountProperty, value); }
    public Color  BadgeColor { get => GetValue(BadgeColorProperty); set => SetValue(BadgeColorProperty, value); }
    public double BadgeSize  { get => GetValue(BadgeSizeProperty);  set => SetValue(BadgeSizeProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        if (BadgeCount <= 0) return;
        var sz  = BadgeSize;
        var cx  = Bounds.Width  / 2;
        var cy  = Bounds.Height / 2;
        var r   = sz / 2.0 - 0.5;
        ctx.DrawEllipse(new SolidColorBrush(BadgeColor), null, new Point(cx, cy), r, r);

        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        var ft  = new FormattedText(BadgeCount.ToString(),
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default,
            Math.Min(dfh * 0.85, sz * 0.55),
            new SolidColorBrush(Colors.White));
        ctx.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var sz = BadgeSize;
        return new Size(sz, sz);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// MissionDistanceBar  (#169 — total mission distance / ETA / max-alt info strip)
// Horizontal read-only bar rendered at the bottom of the Plan map view.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MissionDistanceBar : Control
{
    public static readonly StyledProperty<string> TotalDistanceStrProperty =
        AvaloniaProperty.Register<MissionDistanceBar, string>(nameof(TotalDistanceStr), "—");
    public static readonly StyledProperty<string> TotalTimeStrProperty =
        AvaloniaProperty.Register<MissionDistanceBar, string>(nameof(TotalTimeStr), "—");
    public static readonly StyledProperty<string> MaxAltStrProperty =
        AvaloniaProperty.Register<MissionDistanceBar, string>(nameof(MaxAltStr), "—");
    public static readonly StyledProperty<int>    ItemCountProperty =
        AvaloniaProperty.Register<MissionDistanceBar, int>(nameof(ItemCount), 0);

    static MissionDistanceBar()
    {
        AffectsRender<MissionDistanceBar>(TotalDistanceStrProperty, TotalTimeStrProperty,
            MaxAltStrProperty, ItemCountProperty);
    }

    public string TotalDistanceStr { get => GetValue(TotalDistanceStrProperty); set => SetValue(TotalDistanceStrProperty, value); }
    public string TotalTimeStr     { get => GetValue(TotalTimeStrProperty);     set => SetValue(TotalTimeStrProperty, value); }
    public string MaxAltStr        { get => GetValue(MaxAltStrProperty);        set => SetValue(MaxAltStrProperty, value); }
    public int    ItemCount        { get => GetValue(ItemCountProperty);        set => SetValue(ItemCountProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var dfw    = ScreenMetrics.DefaultFontPixelWidth;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var bounds = new Rect(Bounds.Size);
        var h      = bounds.Height;

        ctx.DrawRectangle(new SolidColorBrush(QgcColors.WindowTransparent), null, bounds);

        double x = dfw;
        void DrawPair(string label, string value)
        {
            var ftL = new FormattedText(label + ": ", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.TextSecondary));
            var ftV = new FormattedText(value, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
                new SolidColorBrush(QgcColors.Text));
            ctx.DrawText(ftL, new Point(x, (h - ftL.Height) / 2));
            ctx.DrawText(ftV, new Point(x + ftL.Width, (h - ftV.Height) / 2));
            x += ftL.Width + ftV.Width + dfw * 1.5;
        }

        DrawPair("Distance", TotalDistanceStr);
        DrawPair("Time", TotalTimeStr);
        DrawPair("Max Alt", MaxAltStr);
        DrawPair("Items", ItemCount.ToString());
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = double.IsInfinity(availableSize.Width) ? ScreenMetrics.DefaultFontPixelWidth * 40 : availableSize.Width;
        return new Size(w, dfh * 1.6);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PlanViewToolBarPanel  (#170 — QGC PlanToolBar: plan view action toolbar)
// TemplatedControl with Upload/Download/Clear/File buttons and dirty indicator.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class PlanViewToolBarPanel : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<bool>   PlanIsDirtyProperty =
        AvaloniaProperty.Register<PlanViewToolBarPanel, bool>(nameof(IsDirty), false);
    public static readonly StyledProperty<bool>   UploadEnabledProperty =
        AvaloniaProperty.Register<PlanViewToolBarPanel, bool>(nameof(UploadEnabled), false);
    public static readonly StyledProperty<bool>   DownloadEnabledProperty =
        AvaloniaProperty.Register<PlanViewToolBarPanel, bool>(nameof(DownloadEnabled), false);
    public static readonly StyledProperty<int>    PlanItemCountProperty =
        AvaloniaProperty.Register<PlanViewToolBarPanel, int>(nameof(PlanItemCount), 0);
    public static readonly StyledProperty<bool>   IsSyncingProperty =
        AvaloniaProperty.Register<PlanViewToolBarPanel, bool>(nameof(IsSyncing), false);

    public bool IsDirty        { get => GetValue(PlanIsDirtyProperty);       set => SetValue(PlanIsDirtyProperty, value); }
    public bool UploadEnabled  { get => GetValue(UploadEnabledProperty);     set => SetValue(UploadEnabledProperty, value); }
    public bool DownloadEnabled{ get => GetValue(DownloadEnabledProperty);   set => SetValue(DownloadEnabledProperty, value); }
    public int  PlanItemCount  { get => GetValue(PlanItemCountProperty);     set => SetValue(PlanItemCountProperty, value); }
    public bool IsSyncing      { get => GetValue(IsSyncingProperty);         set => SetValue(IsSyncingProperty, value); }

    public event EventHandler? UploadRequested;
    public event EventHandler? DownloadRequested;
    public event EventHandler? ClearRequested;
    public event EventHandler? OpenFileRequested;
    public event EventHandler? SaveFileRequested;

    public void RaiseUpload()   => UploadRequested?.Invoke(this, EventArgs.Empty);
    public void RaiseDownload() => DownloadRequested?.Invoke(this, EventArgs.Empty);
    public void RaiseClear()    => ClearRequested?.Invoke(this, EventArgs.Empty);
    public void RaiseOpen()     => OpenFileRequested?.Invoke(this, EventArgs.Empty);
    public void RaiseSave()     => SaveFileRequested?.Invoke(this, EventArgs.Empty);
}

// ─────────────────────────────────────────────────────────────────────────────
// PlanUploadProgressBar  (#171 — mission upload / download item progress)
// Shows the progress of sending or receiving mission items from the vehicle.
// CollapseWhenIdle=true collapses to zero height when not syncing.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class PlanUploadProgressBar : Control
{
    public static readonly StyledProperty<int>    TotalItemsProperty =
        AvaloniaProperty.Register<PlanUploadProgressBar, int>(nameof(TotalItems), 0);
    public static readonly StyledProperty<int>    ProcessedItemsProperty =
        AvaloniaProperty.Register<PlanUploadProgressBar, int>(nameof(ProcessedItems), 0);
    public static readonly StyledProperty<bool>   PlanIsUploadingProperty =
        AvaloniaProperty.Register<PlanUploadProgressBar, bool>(nameof(IsUploading), true);
    public static readonly StyledProperty<bool>   PlanIsSyncingProperty =
        AvaloniaProperty.Register<PlanUploadProgressBar, bool>(nameof(IsSyncing), false);
    public static readonly StyledProperty<bool>   SyncHasErrorProperty =
        AvaloniaProperty.Register<PlanUploadProgressBar, bool>(nameof(HasError), false);
    public static readonly StyledProperty<string> SyncErrorMessageProperty =
        AvaloniaProperty.Register<PlanUploadProgressBar, string>(nameof(ErrorMessage), string.Empty);

    static PlanUploadProgressBar()
    {
        AffectsRender<PlanUploadProgressBar>(TotalItemsProperty, ProcessedItemsProperty,
            PlanIsUploadingProperty, PlanIsSyncingProperty, SyncHasErrorProperty, SyncErrorMessageProperty);
    }

    public int    TotalItems     { get => GetValue(TotalItemsProperty);      set => SetValue(TotalItemsProperty, value); }
    public int    ProcessedItems { get => GetValue(ProcessedItemsProperty);  set => SetValue(ProcessedItemsProperty, value); }
    public bool   IsUploading    { get => GetValue(PlanIsUploadingProperty); set => SetValue(PlanIsUploadingProperty, value); }
    public bool   IsSyncing      { get => GetValue(PlanIsSyncingProperty);   set => SetValue(PlanIsSyncingProperty, value); }
    public bool   HasError       { get => GetValue(SyncHasErrorProperty);    set => SetValue(SyncHasErrorProperty, value); }
    public string ErrorMessage   { get => GetValue(SyncErrorMessageProperty);set => SetValue(SyncErrorMessageProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        if (!IsSyncing && !HasError) return;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;
        var dfw    = ScreenMetrics.DefaultFontPixelWidth;
        var bounds = new Rect(Bounds.Size);
        var h      = bounds.Height;

        // Background
        var bgColor = HasError ? QgcColors.AlertBackground : QgcColors.WindowShade;
        ctx.DrawRectangle(new SolidColorBrush(bgColor), null, bounds);

        if (HasError)
        {
            var ft = new FormattedText(
                string.IsNullOrEmpty(ErrorMessage) ? "Sync error" : ErrorMessage,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.9,
                new SolidColorBrush(QgcColors.AlertText));
            ctx.DrawText(ft, new Point(dfw, (h - ft.Height) / 2));
            return;
        }

        // Progress fill
        double pct  = TotalItems > 0 ? (double)ProcessedItems / TotalItems : 0;
        double fillW = bounds.Width * Math.Clamp(pct, 0, 1);
        ctx.DrawRectangle(new SolidColorBrush(IsUploading ? QgcColors.ColorGreen : QgcColors.ColorBlue),
            null, new Rect(0, 0, fillW, h));

        // Label
        string label = $"{(IsUploading ? "Uploading" : "Downloading")} {ProcessedItems}/{TotalItems}";
        var ftL = new FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85,
            new SolidColorBrush(QgcColors.ButtonText));
        ctx.DrawText(ftL, new Point((bounds.Width - ftL.Width) / 2, (h - ftL.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!IsSyncing && !HasError) return new Size(0, 0);
        var dfh = ScreenMetrics.DefaultFontPixelHeight;
        double w = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
        return new Size(w, dfh * 1.4);
    }
}

// ── #179 TerrainProfileView ───────────────────────────────────────────────────
public class TerrainProfileView : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> TerrainAltitudesProperty =
        AvaloniaProperty.Register<TerrainProfileView, IReadOnlyList<double>?>("TerrainAltitudes");
    public static readonly StyledProperty<double> MaxAltitudeProperty =
        AvaloniaProperty.Register<TerrainProfileView, double>("MaxAltitude", 100.0);
    public static readonly StyledProperty<double> CurrentDistanceRatioProperty =
        AvaloniaProperty.Register<TerrainProfileView, double>("CurrentDistanceRatio", 0.0);
    public static readonly StyledProperty<bool>   ShowFlightPathProperty =
        AvaloniaProperty.Register<TerrainProfileView, bool>("ShowFlightPath", true);

    public IReadOnlyList<double>? TerrainAltitudes    { get => GetValue(TerrainAltitudesProperty);    set => SetValue(TerrainAltitudesProperty, value); }
    public double                 MaxAltitude         { get => GetValue(MaxAltitudeProperty);         set => SetValue(MaxAltitudeProperty, value); }
    public double                 CurrentDistanceRatio{ get => GetValue(CurrentDistanceRatioProperty); set => SetValue(CurrentDistanceRatioProperty, value); }
    public bool                   ShowFlightPath      { get => GetValue(ShowFlightPathProperty);       set => SetValue(ShowFlightPathProperty, value); }

    static TerrainProfileView()
    {
        AffectsRender<TerrainProfileView>(TerrainAltitudesProperty, MaxAltitudeProperty,
            CurrentDistanceRatioProperty, ShowFlightPathProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // Background
        dc.FillRectangle(new SolidColorBrush(QgcColors.WindowShade), new Rect(0, 0, w, h));

        var altitudes = TerrainAltitudes;
        if (altitudes == null || altitudes.Count < 2)
        {
            var noDataFt = new FormattedText("No terrain data", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.85, new SolidColorBrush(QgcColors.TextSecondary));
            dc.DrawText(noDataFt, new Point((w - noDataFt.Width) / 2, (h - noDataFt.Height) / 2));
            return;
        }

        double maxAlt = MaxAltitude > 0 ? MaxAltitude : altitudes.Max();
        if (maxAlt <= 0) maxAlt = 1;

        double padB = dfh * 1.2;
        double padT = 4;
        double chartH = h - padB - padT;
        double chartW = w - 8;
        double stepX  = chartW / (altitudes.Count - 1);

        // Terrain fill polygon
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(4, h - padB), true);
            for (int i = 0; i < altitudes.Count; i++)
            {
                double px = 4 + i * stepX;
                double py = padT + chartH * (1 - altitudes[i] / maxAlt);
                ctx.LineTo(new Point(px, py));
            }
            ctx.LineTo(new Point(4 + chartW, h - padB));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(120, 139, 119, 79)),
            new Pen(new SolidColorBrush(Color.FromArgb(200, 180, 150, 80)), 1.5), geo);

        // Current position marker
        double markerX = 4 + CurrentDistanceRatio * chartW;
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.ColorGreen), 1.5),
            new Point(markerX, padT), new Point(markerX, h - padB));

        // X-axis label
        var xFt = new FormattedText("Distance →", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.7, new SolidColorBrush(QgcColors.TextSecondary));
        dc.DrawText(xFt, new Point(w - xFt.Width - 4, h - dfh * 1.1));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 320;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 6);
    }
}

// ── #180 GeoFencePolygonOverlay ───────────────────────────────────────────────
public class GeoFencePolygonOverlay : Control
{
    public static readonly StyledProperty<IReadOnlyList<Point>?> GFPolyVerticesProperty =
        AvaloniaProperty.Register<GeoFencePolygonOverlay, IReadOnlyList<Point>?>("GFPolyVertices");
    public static readonly StyledProperty<bool> GFPolyIsInclusionProperty =
        AvaloniaProperty.Register<GeoFencePolygonOverlay, bool>("GFPolyIsInclusion", true);
    public static readonly StyledProperty<bool> GFPolyIsSelectedProperty =
        AvaloniaProperty.Register<GeoFencePolygonOverlay, bool>("GFPolyIsSelected", false);
    public static readonly StyledProperty<int>  GFPolyIndexProperty =
        AvaloniaProperty.Register<GeoFencePolygonOverlay, int>("GFPolyIndex", 0);

    public IReadOnlyList<Point>? GFPolyVertices    { get => GetValue(GFPolyVerticesProperty);    set => SetValue(GFPolyVerticesProperty, value); }
    public bool                  GFPolyIsInclusion { get => GetValue(GFPolyIsInclusionProperty); set => SetValue(GFPolyIsInclusionProperty, value); }
    public bool                  GFPolyIsSelected  { get => GetValue(GFPolyIsSelectedProperty);  set => SetValue(GFPolyIsSelectedProperty, value); }
    public int                   GFPolyIndex       { get => GetValue(GFPolyIndexProperty);       set => SetValue(GFPolyIndexProperty, value); }

    static GeoFencePolygonOverlay()
    {
        AffectsRender<GeoFencePolygonOverlay>(GFPolyVerticesProperty, GFPolyIsInclusionProperty,
            GFPolyIsSelectedProperty, GFPolyIndexProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var pts = GFPolyVertices;
        if (pts == null || pts.Count < 3) return;

        Color edgeColor = GFPolyIsInclusion ? QgcColors.ColorGreen : QgcColors.ColorRed;
        Color fillColor = Color.FromArgb(30, edgeColor.R, edgeColor.G, edgeColor.B);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], true);
            for (int i = 1; i < pts.Count; i++) ctx.LineTo(pts[i]);
            ctx.EndFigure(true);
        }

        var dashStyle  = new DashStyle(new double[] { 6, 3 }, 0);
        var strokePen  = new Pen(new SolidColorBrush(edgeColor), GFPolyIsSelected ? 2.5 : 1.5, dashStyle);
        dc.DrawGeometry(new SolidColorBrush(fillColor), strokePen, geo);

        // Corner handles when selected
        if (GFPolyIsSelected)
        {
            var handleBrush = new SolidColorBrush(QgcColors.PrimaryButtonFill);
            var handlePen   = new Pen(new SolidColorBrush(Colors.White), 1);
            foreach (var pt in pts)
                dc.DrawEllipse(handleBrush, handlePen, pt, 5, 5);
        }

        // Index badge at centroid
        double cx = pts.Average(p => p.X);
        double cy = pts.Average(p => p.Y);
        var badgeFt = new FormattedText($"P{GFPolyIndex}",
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default,
            ScreenMetrics.DefaultFontPixelHeight * 0.75,
            new SolidColorBrush(edgeColor));
        dc.DrawText(badgeFt, new Point(cx - badgeFt.Width / 2, cy - badgeFt.Height / 2));
    }
}

// ── #181 MapOverlayLabel ──────────────────────────────────────────────────────
public class MapOverlayLabel : Control
{
    public static readonly StyledProperty<string>  MOLTextProperty =
        AvaloniaProperty.Register<MapOverlayLabel, string>("MOLText", string.Empty);
    public static readonly StyledProperty<Color>   MOLTextColorProperty =
        AvaloniaProperty.Register<MapOverlayLabel, Color>("MOLTextColor", Colors.White);
    public static readonly StyledProperty<bool>    MOLHasBgProperty =
        AvaloniaProperty.Register<MapOverlayLabel, bool>("MOLHasBg", true);
    public static readonly StyledProperty<double>  MOLFontSizeProperty =
        AvaloniaProperty.Register<MapOverlayLabel, double>("MOLFontSize", 0.0);

    public string MOLText      { get => GetValue(MOLTextProperty);      set => SetValue(MOLTextProperty, value); }
    public Color  MOLTextColor { get => GetValue(MOLTextColorProperty); set => SetValue(MOLTextColorProperty, value); }
    public bool   MOLHasBg    { get => GetValue(MOLHasBgProperty);     set => SetValue(MOLHasBgProperty, value); }
    public double MOLFontSize  { get => GetValue(MOLFontSizeProperty);  set => SetValue(MOLFontSizeProperty, value); }

    static MapOverlayLabel()
    {
        AffectsRender<MapOverlayLabel>(MOLTextProperty, MOLTextColorProperty, MOLHasBgProperty, MOLFontSizeProperty);
    }

    public override void Render(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(MOLText)) return;
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        double fs  = MOLFontSize > 0 ? MOLFontSize : ScreenMetrics.DefaultFontPixelHeight * 0.85;
        double br  = ScreenMetrics.DefaultBorderRadius;

        if (MOLHasBg)
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowTransparent), null,
                new Rect(0, 0, w, h), br);

        var ft = new FormattedText(MOLText, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, fs,
            new SolidColorBrush(MOLTextColor));
        dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double fs  = MOLFontSize > 0 ? MOLFontSize : ScreenMetrics.DefaultFontPixelHeight * 0.85;
        var ft = new FormattedText(
            string.IsNullOrEmpty(MOLText) ? " " : MOLText,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, fs,
            new SolidColorBrush(Colors.White));
        return new Size(ft.Width + 12, ft.Height + 6);
    }
}

// ── #200 WaypointEditRow ──────────────────────────────────────────────────────
public class WaypointEditRow : Control
{
    public static readonly StyledProperty<string> WPCommandProperty =
        AvaloniaProperty.Register<WaypointEditRow, string>("WPCommand", "NAV_WAYPOINT");
    public static readonly StyledProperty<int>    WPSequenceProperty =
        AvaloniaProperty.Register<WaypointEditRow, int>("WPSequence", 0);
    public static readonly StyledProperty<double> WPLatProperty =
        AvaloniaProperty.Register<WaypointEditRow, double>("WPLat", 0.0);
    public static readonly StyledProperty<double> WPLonProperty =
        AvaloniaProperty.Register<WaypointEditRow, double>("WPLon", 0.0);
    public static readonly StyledProperty<double> WPAltProperty =
        AvaloniaProperty.Register<WaypointEditRow, double>("WPAlt", 0.0);
    public static readonly StyledProperty<bool>   WPIsCurrentProperty =
        AvaloniaProperty.Register<WaypointEditRow, bool>("WPIsCurrent", false);
    public static readonly StyledProperty<bool>   WPIsSelectedProperty =
        AvaloniaProperty.Register<WaypointEditRow, bool>("WPIsSelected", false);

    public string WPCommand    { get => GetValue(WPCommandProperty);    set => SetValue(WPCommandProperty, value); }
    public int    WPSequence   { get => GetValue(WPSequenceProperty);   set => SetValue(WPSequenceProperty, value); }
    public double WPLat        { get => GetValue(WPLatProperty);        set => SetValue(WPLatProperty, value); }
    public double WPLon        { get => GetValue(WPLonProperty);        set => SetValue(WPLonProperty, value); }
    public double WPAlt        { get => GetValue(WPAltProperty);        set => SetValue(WPAltProperty, value); }
    public bool   WPIsCurrent  { get => GetValue(WPIsCurrentProperty);  set => SetValue(WPIsCurrentProperty, value); }
    public bool   WPIsSelected { get => GetValue(WPIsSelectedProperty); set => SetValue(WPIsSelectedProperty, value); }

    static WaypointEditRow()
    {
        AffectsRender<WaypointEditRow>(WPCommandProperty, WPSequenceProperty, WPLatProperty,
            WPLonProperty, WPAltProperty, WPIsCurrentProperty, WPIsSelectedProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        double w   = bounds.Width;
        double h   = bounds.Height;
        var dfh    = ScreenMetrics.DefaultFontPixelHeight;

        // Row background
        Color bg = WPIsSelected ? Color.FromArgb(30, 30, 120, 255)
                 : WPIsCurrent  ? Color.FromArgb(20, 0, 200, 80)
                                : QgcColors.Window;
        dc.FillRectangle(new SolidColorBrush(bg), new Rect(0, 0, w, h));
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Current indicator triangle
        if (WPIsCurrent)
        {
            var triGeo = new StreamGeometry();
            using (var ctx = triGeo.Open())
            {
                ctx.BeginFigure(new Point(0, h / 2), true);
                ctx.LineTo(new Point(5, h / 2 - 5));
                ctx.LineTo(new Point(5, h / 2 + 5));
                ctx.EndFigure(true);
            }
            dc.DrawGeometry(new SolidColorBrush(QgcColors.ColorGreen), null, triGeo);
        }

        double x = 8;

        // Sequence badge
        var seqFt = new FormattedText($"{WPSequence}", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(QgcColors.Text));
        double badgeW = Math.Max(seqFt.Width + 8, dfh * 1.4);
        double badgeH = dfh * 1.1;
        double badgeY = (h - badgeH) / 2;
        double br     = ScreenMetrics.DefaultBorderRadius;
        dc.DrawRectangle(new SolidColorBrush(WPIsCurrent ? QgcColors.ColorGreen : QgcColors.Button),
            null, new Rect(x, badgeY, badgeW, badgeH), br);
        dc.DrawText(seqFt, new Point(x + (badgeW - seqFt.Width) / 2, badgeY + (badgeH - seqFt.Height) / 2));
        x += badgeW + 6;

        // Command name
        var cmdFt = new FormattedText(WPCommand, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, Typeface.Default, dfh * 0.78,
            new SolidColorBrush(QgcColors.Text));
        dc.DrawText(cmdFt, new Point(x, (h - cmdFt.Height) / 2));
        x += w * 0.22;

        // Lat / Lon / Alt
        (string label, double val)[] coords = { ("Lat", WPLat), ("Lon", WPLon), ("Alt", WPAlt) };
        double colW = (w - x - 8) / 3;
        for (int i = 0; i < 3; i++)
        {
            var hdr = new FormattedText(coords[i].label, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.65,
                new SolidColorBrush(QgcColors.TextSecondary));
            var val = new FormattedText($"{coords[i].val:F5}", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.75,
                new SolidColorBrush(QgcColors.Text));
            double cx = x + i * colW;
            dc.DrawText(hdr, new Point(cx, h * 0.10));
            dc.DrawText(val, new Point(cx, h * 0.45));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = !double.IsInfinity(availableSize.Width) ? availableSize.Width : 400;
        return new Size(w, ScreenMetrics.DefaultFontPixelHeight * 2.4);
    }
}

// ── #208 MissionLineSegment ───────────────────────────────────────────────────
public class MissionLineSegment : Control
{
    public static readonly StyledProperty<Point>  MLSStartPtProperty =
        AvaloniaProperty.Register<MissionLineSegment, Point>("MLSStartPt", default);
    public static readonly StyledProperty<Point>  MLSEndPtProperty =
        AvaloniaProperty.Register<MissionLineSegment, Point>("MLSEndPt", default);
    public static readonly StyledProperty<double> MLSDistanceMProperty =
        AvaloniaProperty.Register<MissionLineSegment, double>("MLSDistanceM", 0.0);
    public static readonly StyledProperty<bool>   MLSIsActiveProperty =
        AvaloniaProperty.Register<MissionLineSegment, bool>("MLSIsActive", false);

    public Point  MLSStartPt   { get => GetValue(MLSStartPtProperty);   set => SetValue(MLSStartPtProperty, value); }
    public Point  MLSEndPt     { get => GetValue(MLSEndPtProperty);     set => SetValue(MLSEndPtProperty, value); }
    public double MLSDistanceM { get => GetValue(MLSDistanceMProperty); set => SetValue(MLSDistanceMProperty, value); }
    public bool   MLSIsActive  { get => GetValue(MLSIsActiveProperty);  set => SetValue(MLSIsActiveProperty, value); }

    static MissionLineSegment()
    {
        AffectsRender<MissionLineSegment>(MLSStartPtProperty, MLSEndPtProperty,
            MLSDistanceMProperty, MLSIsActiveProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var s   = MLSStartPt;
        var e   = MLSEndPt;
        var dfh = ScreenMetrics.DefaultFontPixelHeight;

        Color lineColor = MLSIsActive ? QgcColors.ColorGreen : QgcColors.ColorBlue;
        double lineW    = MLSIsActive ? 2.5 : 1.5;
        dc.DrawLine(new Pen(new SolidColorBrush(lineColor), lineW), s, e);

        // Midpoint distance label
        if (MLSDistanceM > 0)
        {
            double mx  = (s.X + e.X) / 2;
            double my  = (s.Y + e.Y) / 2;
            string dist = MLSDistanceM >= 1000
                ? $"{MLSDistanceM / 1000:F2} km"
                : $"{MLSDistanceM:F0} m";
            var ft = new FormattedText(dist, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Typeface.Default, dfh * 0.72,
                new SolidColorBrush(Colors.White));
            // Small background pill
            double pad = 3;
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), null,
                new Rect(mx - ft.Width / 2 - pad, my - ft.Height / 2 - pad,
                         ft.Width + pad * 2, ft.Height + pad * 2), 3);
            dc.DrawText(ft, new Point(mx - ft.Width / 2, my - ft.Height / 2));
        }
    }
}

// ── #209 SurveyAreaOverlay ────────────────────────────────────────────────────
public class SurveyAreaOverlay : Control
{
    public static readonly StyledProperty<IReadOnlyList<Point>?> SABoundsPathProperty =
        AvaloniaProperty.Register<SurveyAreaOverlay, IReadOnlyList<Point>?>("SABoundsPath");
    public static readonly StyledProperty<double> SAGridSpacingProperty =
        AvaloniaProperty.Register<SurveyAreaOverlay, double>("SAGridSpacing", 30.0);
    public static readonly StyledProperty<double> SAAngleProperty =
        AvaloniaProperty.Register<SurveyAreaOverlay, double>("SAAngle", 0.0);
    public static readonly StyledProperty<bool>   SAIsSelectedProperty =
        AvaloniaProperty.Register<SurveyAreaOverlay, bool>("SAIsSelected", false);

    public IReadOnlyList<Point>? SABoundsPath { get => GetValue(SABoundsPathProperty); set => SetValue(SABoundsPathProperty, value); }
    public double                SAGridSpacing{ get => GetValue(SAGridSpacingProperty); set => SetValue(SAGridSpacingProperty, value); }
    public double                SAAngle      { get => GetValue(SAAngleProperty);       set => SetValue(SAAngleProperty, value); }
    public bool                  SAIsSelected { get => GetValue(SAIsSelectedProperty);  set => SetValue(SAIsSelectedProperty, value); }

    static SurveyAreaOverlay()
    {
        AffectsRender<SurveyAreaOverlay>(SABoundsPathProperty, SAGridSpacingProperty,
            SAAngleProperty, SAIsSelectedProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var pts = SABoundsPath;
        if (pts == null || pts.Count < 3) return;

        Color edgeColor = QgcColors.ColorGreen;
        Color fillColor = Color.FromArgb(25, edgeColor.R, edgeColor.G, edgeColor.B);

        // Boundary polygon
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], true);
            for (int i = 1; i < pts.Count; i++) ctx.LineTo(pts[i]);
            ctx.EndFigure(true);
        }
        var strokePen = new Pen(new SolidColorBrush(edgeColor), SAIsSelected ? 2.5 : 1.5);
        dc.DrawGeometry(new SolidColorBrush(fillColor), strokePen, geo);

        // Grid hatch lines inside bounding box (simplified — axis-aligned)
        double minX = pts.Min(p => p.X);
        double maxX = pts.Max(p => p.X);
        double minY = pts.Min(p => p.Y);
        double maxY = pts.Max(p => p.Y);
        double spacing = Math.Max(8, SAGridSpacing);
        var hatchPen = new Pen(new SolidColorBrush(Color.FromArgb(60, edgeColor.R, edgeColor.G, edgeColor.B)), 0.8);
        for (double y = minY; y <= maxY; y += spacing)
            dc.DrawLine(hatchPen, new Point(minX, y), new Point(maxX, y));

        // Corner handles when selected
        if (SAIsSelected)
        {
            var handleBrush = new SolidColorBrush(QgcColors.PrimaryButtonFill);
            var handlePen   = new Pen(new SolidColorBrush(Colors.White), 1);
            foreach (var pt in pts)
                dc.DrawEllipse(handleBrush, handlePen, pt, 5, 5);
        }
    }
}

// ── #219 MissionItemEditor ────────────────────────────────────────────────────
// Compact inline editor row for a single mission item: sequence badge + command
// name + up to 3 numeric param fields side by side.
// MIEIndex, MIECommandName, MIEParam1/2/3, MIEIsSelected.
// Raises ParamChanged(paramIndex 0-2, newValue) when a param field is tapped.
public sealed class MissionItemEditor : Control
{
    public static readonly StyledProperty<int>    MIEIndexProperty =
        AvaloniaProperty.Register<MissionItemEditor, int>("MIEIndex", 0);
    public static readonly StyledProperty<string> MIECommandNameProperty =
        AvaloniaProperty.Register<MissionItemEditor, string>("MIECommandName", "NAV_WAYPOINT");
    public static readonly StyledProperty<double> MIEParam1Property =
        AvaloniaProperty.Register<MissionItemEditor, double>("MIEParam1", 0.0);
    public static readonly StyledProperty<double> MIEParam2Property =
        AvaloniaProperty.Register<MissionItemEditor, double>("MIEParam2", 0.0);
    public static readonly StyledProperty<double> MIEParam3Property =
        AvaloniaProperty.Register<MissionItemEditor, double>("MIEParam3", 0.0);
    public static readonly StyledProperty<bool>   MIEIsSelectedProperty =
        AvaloniaProperty.Register<MissionItemEditor, bool>("MIEIsSelected", false);

    static MissionItemEditor()
    {
        AffectsRender<MissionItemEditor>(MIEIndexProperty, MIECommandNameProperty,
            MIEParam1Property, MIEParam2Property, MIEParam3Property, MIEIsSelectedProperty);
    }

    public int    MIEIndex       { get => GetValue(MIEIndexProperty);       set => SetValue(MIEIndexProperty, value); }
    public string MIECommandName { get => GetValue(MIECommandNameProperty); set => SetValue(MIECommandNameProperty, value); }
    public double MIEParam1      { get => GetValue(MIEParam1Property);      set => SetValue(MIEParam1Property, value); }
    public double MIEParam2      { get => GetValue(MIEParam2Property);      set => SetValue(MIEParam2Property, value); }
    public double MIEParam3      { get => GetValue(MIEParam3Property);      set => SetValue(MIEParam3Property, value); }
    public bool   MIEIsSelected  { get => GetValue(MIEIsSelectedProperty);  set => SetValue(MIEIsSelectedProperty, value); }

    public event EventHandler<(int ParamIndex, double NewValue)>? ParamChanged;

    private readonly Rect[] _paramRects = new Rect[3];

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double dfw = ScreenMetrics.DefaultFontPixelWidth;
        double br  = ScreenMetrics.DefaultBorderRadius;

        // Row background
        if (MIEIsSelected)
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 0, 160, 255)), null,
                new Rect(0, 0, w, h));
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));

        // Sequence badge
        double badgeR = h * 0.32;
        Color  badgeC = MIEIsSelected ? QgcColors.PrimaryButtonFill : QgcColors.ColorGrey;
        dc.DrawEllipse(new SolidColorBrush(badgeC), null, new Point(badgeR + 4, h / 2), badgeR, badgeR);
        var idxFt = new FormattedText(MIEIndex.ToString(),
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.72, new SolidColorBrush(Colors.White));
        dc.DrawText(idxFt, new Point(badgeR + 4 - idxFt.Width / 2, h / 2 - idxFt.Height / 2));

        // Command name
        double cmdX = badgeR * 2 + 10;
        double cmdW = w * 0.30;
        var cmdFt = new FormattedText(MIECommandName,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.78, new SolidColorBrush(QgcColors.Text))
        { MaxTextWidth = cmdW };
        dc.DrawText(cmdFt, new Point(cmdX, (h - cmdFt.Height) / 2));

        // Param fields (3 equal columns in remaining space)
        double paramStart = cmdX + cmdW + dfw;
        double paramW     = (w - paramStart - dfw) / 3;
        double[] pvals    = [MIEParam1, MIEParam2, MIEParam3];
        for (int i = 0; i < 3; i++)
        {
            double px = paramStart + i * (paramW + 2);
            var pr = new Rect(px, 2, paramW, h - 4);
            _paramRects[i] = pr;
            dc.DrawRectangle(new SolidColorBrush(QgcColors.WindowShade),
                new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5), pr, br, br);
            var pFt = new FormattedText($"{pvals[i]:F1}",
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.75, new SolidColorBrush(QgcColors.Text));
            dc.DrawText(pFt, new Point(px + (paramW - pFt.Width) / 2, (h - pFt.Height) / 2));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        for (int i = 0; i < 3; i++)
        {
            if (_paramRects[i].Width > 0 && _paramRects[i].Contains(pos))
            {
                double[] pvals = [MIEParam1, MIEParam2, MIEParam3];
                ParamChanged?.Invoke(this, (i, pvals[i]));
                e.Handled = true;
                return;
            }
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 360 : available.Width;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight + 4);
    }
}

// ── #220 SafetyZoneOverlay ────────────────────────────────────────────────────
// Red translucent circle representing a safety/exclusion radius on the map.
// SZCenterX/Y are pixel coords within the control; SZRadiusPx is the circle radius.
// SZIsExclusion: exclusion zone (solid red fill) vs inclusion (green outline).
public sealed class SafetyZoneOverlay : Control
{
    public static readonly StyledProperty<double> SZCenterXProperty =
        AvaloniaProperty.Register<SafetyZoneOverlay, double>("SZCenterX", 0.0);
    public static readonly StyledProperty<double> SZCenterYProperty =
        AvaloniaProperty.Register<SafetyZoneOverlay, double>("SZCenterY", 0.0);
    public static readonly StyledProperty<double> SZRadiusPxProperty =
        AvaloniaProperty.Register<SafetyZoneOverlay, double>("SZRadiusPx", 50.0);
    public static readonly StyledProperty<bool>   SZIsExclusionProperty =
        AvaloniaProperty.Register<SafetyZoneOverlay, bool>("SZIsExclusion", true);
    public static readonly StyledProperty<bool>   SZIsSelectedProperty =
        AvaloniaProperty.Register<SafetyZoneOverlay, bool>("SZIsSelected", false);
    public static readonly StyledProperty<string> SZLabelProperty =
        AvaloniaProperty.Register<SafetyZoneOverlay, string>("SZLabel", string.Empty);

    static SafetyZoneOverlay()
    {
        AffectsRender<SafetyZoneOverlay>(SZCenterXProperty, SZCenterYProperty,
            SZRadiusPxProperty, SZIsExclusionProperty, SZIsSelectedProperty, SZLabelProperty);
    }

    public double SZCenterX    { get => GetValue(SZCenterXProperty);    set => SetValue(SZCenterXProperty, value); }
    public double SZCenterY    { get => GetValue(SZCenterYProperty);    set => SetValue(SZCenterYProperty, value); }
    public double SZRadiusPx   { get => GetValue(SZRadiusPxProperty);   set => SetValue(SZRadiusPxProperty, value); }
    public bool   SZIsExclusion{ get => GetValue(SZIsExclusionProperty);set => SetValue(SZIsExclusionProperty, value); }
    public bool   SZIsSelected { get => GetValue(SZIsSelectedProperty); set => SetValue(SZIsSelectedProperty, value); }
    public string SZLabel      { get => GetValue(SZLabelProperty);      set => SetValue(SZLabelProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double cx = SZCenterX;
        double cy = SZCenterY;
        double r  = SZRadiusPx;
        if (r <= 0) return;

        Color fillC    = SZIsExclusion ? Color.FromArgb(40, 220, 40, 40) : Color.FromArgb(40, 40, 200, 40);
        Color strokeC  = SZIsExclusion ? QgcColors.ColorRed : QgcColors.ColorGreen;
        double strokeW = SZIsSelected ? 2.5 : 1.5;

        dc.DrawEllipse(new SolidColorBrush(fillC),
            new Pen(new SolidColorBrush(strokeC), strokeW),
            new Point(cx, cy), r, r);

        // Handle dot at top
        dc.DrawEllipse(new SolidColorBrush(SZIsSelected ? Colors.White : strokeC), null,
            new Point(cx, cy - r), 4, 4);

        // Label
        if (!string.IsNullOrEmpty(SZLabel))
        {
            double dfh = ScreenMetrics.DefaultFontPixelHeight;
            var ft = new FormattedText(SZLabel,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.75, new SolidColorBrush(strokeC));
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }
    }

    protected override Size MeasureOverride(Size available) =>
        double.IsInfinity(available.Width) ? new Size(200, 200) : available;
}

// ── #230 AircraftTrailOverlay ─────────────────────────────────────────────────
// Renders a position-history trail as a fading polyline.  ATOPoints is a list
// of (X, Y) pixel coordinates; oldest points are drawn transparent, newest opaque.
// ATOColor sets the line base colour; ATOMaxPoints limits buffer length.
public sealed class AircraftTrailOverlay : Control
{
    public static readonly StyledProperty<IReadOnlyList<Point>?> ATOPointsProperty =
        AvaloniaProperty.Register<AircraftTrailOverlay, IReadOnlyList<Point>?>("ATOPoints", null);
    public static readonly StyledProperty<Color> ATOColorProperty =
        AvaloniaProperty.Register<AircraftTrailOverlay, Color>("ATOColor", Color.FromRgb(0, 180, 255));
    public static readonly StyledProperty<double> ATOLineWidthProperty =
        AvaloniaProperty.Register<AircraftTrailOverlay, double>("ATOLineWidth", 2.0);

    static AircraftTrailOverlay()
    {
        AffectsRender<AircraftTrailOverlay>(ATOPointsProperty, ATOColorProperty, ATOLineWidthProperty);
    }

    public IReadOnlyList<Point>? ATOPoints    { get => GetValue(ATOPointsProperty);    set => SetValue(ATOPointsProperty, value); }
    public Color                 ATOColor     { get => GetValue(ATOColorProperty);     set => SetValue(ATOColorProperty, value); }
    public double                ATOLineWidth { get => GetValue(ATOLineWidthProperty); set => SetValue(ATOLineWidthProperty, value); }

    public override void Render(DrawingContext dc)
    {
        var pts = ATOPoints;
        if (pts == null || pts.Count < 2) return;

        int count = pts.Count;
        for (int i = 1; i < count; i++)
        {
            // Alpha fades from 20 (oldest) to 220 (newest)
            byte alpha = (byte)(20 + (200 * i / (count - 1)));
            var pen = new Pen(new SolidColorBrush(
                Color.FromArgb(alpha, ATOColor.R, ATOColor.G, ATOColor.B)),
                ATOLineWidth, lineCap: PenLineCap.Round);
            dc.DrawLine(pen, pts[i - 1], pts[i]);
        }
    }

    protected override Size MeasureOverride(Size available) =>
        double.IsInfinity(available.Width) ? new Size(400, 400) : available;
}

// ── #231 TakeoffLandingMarker ─────────────────────────────────────────────────
// Pin-style marker for takeoff (T) or landing (L) points on the map.
// TLMIsTakeoff selects colour (green=takeoff, orange=landing) and letter.
// TLMCenterX/Y are pixel coords; the pin is drawn as a circle + stem.
public sealed class TakeoffLandingMarker : Control
{
    public static readonly StyledProperty<double> TLMCenterXProperty =
        AvaloniaProperty.Register<TakeoffLandingMarker, double>("TLMCenterX", 0.0);
    public static readonly StyledProperty<double> TLMCenterYProperty =
        AvaloniaProperty.Register<TakeoffLandingMarker, double>("TLMCenterY", 0.0);
    public static readonly StyledProperty<bool>   TLMIsTakeoffProperty =
        AvaloniaProperty.Register<TakeoffLandingMarker, bool>("TLMIsTakeoff", true);
    public static readonly StyledProperty<double> TLMScaleProperty =
        AvaloniaProperty.Register<TakeoffLandingMarker, double>("TLMScale", 1.0);

    static TakeoffLandingMarker()
    {
        AffectsRender<TakeoffLandingMarker>(TLMCenterXProperty, TLMCenterYProperty,
                                             TLMIsTakeoffProperty, TLMScaleProperty);
    }

    public double TLMCenterX  { get => GetValue(TLMCenterXProperty);  set => SetValue(TLMCenterXProperty, value); }
    public double TLMCenterY  { get => GetValue(TLMCenterYProperty);  set => SetValue(TLMCenterYProperty, value); }
    public bool   TLMIsTakeoff{ get => GetValue(TLMIsTakeoffProperty);set => SetValue(TLMIsTakeoffProperty, value); }
    public double TLMScale    { get => GetValue(TLMScaleProperty);    set => SetValue(TLMScaleProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double cx    = TLMCenterX;
        double cy    = TLMCenterY;
        double sc    = Math.Clamp(TLMScale, 0.5, 3.0);
        double r     = 12 * sc;
        double stemH = 16 * sc;
        Color  fillC = TLMIsTakeoff ? QgcColors.ColorGreen : QgcColors.ColorOrange;
        string letter= TLMIsTakeoff ? "T" : "L";

        // Stem (line from bottom of circle down)
        var stemPen = new Pen(new SolidColorBrush(fillC), 2 * sc);
        dc.DrawLine(stemPen, new Point(cx, cy + r), new Point(cx, cy + r + stemH));

        // Circle
        dc.DrawEllipse(new SolidColorBrush(fillC),
            new Pen(new SolidColorBrush(Colors.White), 1.5 * sc),
            new Point(cx, cy), r, r);

        // Letter
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        var ft = new FormattedText(letter,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.Bold),
            dfh * 0.85 * sc, new SolidColorBrush(Colors.White));
        dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }

    protected override Size MeasureOverride(Size available) =>
        double.IsInfinity(available.Width) ? new Size(400, 400) : available;
}

// ── #241 MissionAltitudeDisplay ───────────────────────────────────────────────
// Altitude callout box positioned at a map waypoint: rounded rect with altitude
// value (large) + frame tag (Rel/AMSL/Terrain, small) + optional direction arrow.
// MADCenterX/Y are pixel coords of the waypoint; MADAltMetres and MADAltFrame
// drive the text.  MADShowArrowDown draws a downward stem connecting to the point.
public sealed class MissionAltitudeDisplay : Control
{
    public static readonly StyledProperty<double> MADCenterXProperty =
        AvaloniaProperty.Register<MissionAltitudeDisplay, double>("MADCenterX", 0.0);
    public static readonly StyledProperty<double> MADCenterYProperty =
        AvaloniaProperty.Register<MissionAltitudeDisplay, double>("MADCenterY", 0.0);
    public static readonly StyledProperty<double> MADAltMetresProperty =
        AvaloniaProperty.Register<MissionAltitudeDisplay, double>("MADAltMetres", 0.0);
    public static readonly StyledProperty<string> MADAltFrameProperty =
        AvaloniaProperty.Register<MissionAltitudeDisplay, string>("MADAltFrame", "Rel");
    public static readonly StyledProperty<bool>   MADShowArrowDownProperty =
        AvaloniaProperty.Register<MissionAltitudeDisplay, bool>("MADShowArrowDown", true);

    static MissionAltitudeDisplay()
    {
        AffectsRender<MissionAltitudeDisplay>(MADCenterXProperty, MADCenterYProperty,
            MADAltMetresProperty, MADAltFrameProperty, MADShowArrowDownProperty);
    }

    public double MADCenterX     { get => GetValue(MADCenterXProperty);     set => SetValue(MADCenterXProperty, value); }
    public double MADCenterY     { get => GetValue(MADCenterYProperty);     set => SetValue(MADCenterYProperty, value); }
    public double MADAltMetres   { get => GetValue(MADAltMetresProperty);   set => SetValue(MADAltMetresProperty, value); }
    public string MADAltFrame    { get => GetValue(MADAltFrameProperty);    set => SetValue(MADAltFrameProperty, value); }
    public bool   MADShowArrowDown{ get => GetValue(MADShowArrowDownProperty);set => SetValue(MADShowArrowDownProperty, value); }

    public override void Render(DrawingContext dc)
    {
        double cx  = MADCenterX;
        double cy  = MADCenterY;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double br  = ScreenMetrics.DefaultBorderRadius;
        double pad = 5;

        string altTxt   = $"{MADAltMetres:F1} m";
        string frameTxt = MADAltFrame;

        var altFt = new FormattedText(altTxt,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold),
            dfh * 0.88, new SolidColorBrush(Colors.White));
        var frameFt = new FormattedText(frameTxt,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, dfh * 0.62, new SolidColorBrush(Color.FromRgb(180, 220, 255)));

        double boxW = Math.Max(altFt.Width, frameFt.Width) + pad * 2;
        double boxH = altFt.Height + frameFt.Height + pad;
        double stemH = MADShowArrowDown ? 10 : 0;

        double boxX = cx - boxW / 2;
        double boxY = cy - boxH - stemH;

        // Box background
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(210, 30, 50, 90)),
            new Pen(new SolidColorBrush(Color.FromRgb(80, 140, 220)), 1),
            new Rect(boxX, boxY, boxW, boxH), br, br);

        // Text
        dc.DrawText(altFt,   new Point(boxX + (boxW - altFt.Width) / 2,   boxY + pad * 0.5));
        dc.DrawText(frameFt, new Point(boxX + (boxW - frameFt.Width) / 2, boxY + altFt.Height + 1));

        // Arrow stem
        if (MADShowArrowDown)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(80, 140, 220)), 1.5);
            dc.DrawLine(pen, new Point(cx, boxY + boxH), new Point(cx, cy));
            dc.DrawLine(pen, new Point(cx - 4, cy - 4), new Point(cx, cy));
            dc.DrawLine(pen, new Point(cx + 4, cy - 4), new Point(cx, cy));
        }
    }

    protected override Size MeasureOverride(Size available) =>
        double.IsInfinity(available.Width) ? new Size(400, 400) : available;
}

// ══════════════════════════════════════════════════════════════════
// #248 — MissionCommandSummary
// QGC: src/QmlControls/MissionCommandSummary.qml
// Single row in the PlanView mission list: index badge + command name +
// truncated parameter summary.  Click → SelectionRequested, double-click
// → EditRequested.
// ══════════════════════════════════════════════════════════════════

public sealed class MissionCommandSummary : Control
{
    public static readonly StyledProperty<int>    MCSIndexProperty =
        AvaloniaProperty.Register<MissionCommandSummary, int>("MCSIndex", 0);
    public static readonly StyledProperty<string> MCSCommandNameProperty =
        AvaloniaProperty.Register<MissionCommandSummary, string>("MCSCommandName", "");
    public static readonly StyledProperty<string> MCSParamSummaryProperty =
        AvaloniaProperty.Register<MissionCommandSummary, string>("MCSParamSummary", "");
    public static readonly StyledProperty<bool>   MCSIsSelectedProperty =
        AvaloniaProperty.Register<MissionCommandSummary, bool>("MCSIsSelected", false);
    public static readonly StyledProperty<Color>  MCSBadgeColorProperty =
        AvaloniaProperty.Register<MissionCommandSummary, Color>("MCSBadgeColor", default);

    public int    MCSIndex        { get => GetValue(MCSIndexProperty);        set => SetValue(MCSIndexProperty, value); }
    public string MCSCommandName  { get => GetValue(MCSCommandNameProperty);  set => SetValue(MCSCommandNameProperty, value); }
    public string MCSParamSummary { get => GetValue(MCSParamSummaryProperty); set => SetValue(MCSParamSummaryProperty, value); }
    public bool   MCSIsSelected   { get => GetValue(MCSIsSelectedProperty);   set => SetValue(MCSIsSelectedProperty, value); }
    public Color  MCSBadgeColor   { get => GetValue(MCSBadgeColorProperty);   set => SetValue(MCSBadgeColorProperty, value); }

    public event EventHandler<int>? SelectionRequested;
    public event EventHandler<int>? EditRequested;

    private DateTime _lastClick = DateTime.MinValue;

    static MissionCommandSummary()
    {
        AffectsRender<MissionCommandSummary>(MCSIndexProperty, MCSCommandNameProperty,
            MCSParamSummaryProperty, MCSIsSelectedProperty, MCSBadgeColorProperty);
    }

    public override void Render(DrawingContext dc)
    {
        double w   = Bounds.Width;
        double h   = Bounds.Height;
        double dfh = ScreenMetrics.DefaultFontPixelHeight;
        double pad = 8;

        // Row background
        Color bgC = MCSIsSelected
            ? Color.FromArgb(60, 255, 165, 0)
            : Color.FromArgb(0, 0, 0, 0);
        if (bgC.A > 0)
            dc.DrawRectangle(new SolidColorBrush(bgC), null, new Rect(0, 0, w, h));

        // Index badge (coloured circle with white number)
        double badgeR = Math.Min(h * 0.38, 14);
        double badgeX = pad + badgeR;
        double badgeCy = h / 2;
        Color  badgeC = MCSBadgeColor == default ? QgcColors.ColorBlue : MCSBadgeColor;
        dc.DrawEllipse(new SolidColorBrush(badgeC), null, new Point(badgeX, badgeCy), badgeR, badgeR);
        var idxFt = new FormattedText(MCSIndex.ToString(),
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, weight: FontWeight.Bold),
            dfh * 0.65, new SolidColorBrush(Colors.White));
        dc.DrawText(idxFt, new Point(badgeX - idxFt.Width / 2, badgeCy - idxFt.Height / 2));

        // Command name
        double textX = badgeX + badgeR + pad;
        double nameW = w * 0.45;
        var nameFt = new FormattedText(MCSCommandName,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, weight: FontWeight.Medium),
            dfh * 0.85, new SolidColorBrush(QgcColors.Text));
        using var nameClip = dc.PushClip(new Rect(textX, 0, nameW, h));
        dc.DrawText(nameFt, new Point(textX, (h - nameFt.Height) / 2));

        // Parameter summary (right-aligned, truncated, grey)
        double summaryX = textX + nameW + pad;
        double summaryW = w - summaryX - pad;
        if (!string.IsNullOrEmpty(MCSParamSummary) && summaryW > 20)
        {
            var sumFt = new FormattedText(MCSParamSummary,
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, dfh * 0.75, new SolidColorBrush(QgcColors.TextSecondary));
            using var sumClip = dc.PushClip(new Rect(summaryX, 0, summaryW, h));
            dc.DrawText(sumFt, new Point(summaryX, (h - sumFt.Height) / 2));
        }

        // Bottom separator
        dc.DrawLine(new Pen(new SolidColorBrush(QgcColors.GroupBorder), 0.5),
            new Point(0, h - 0.5), new Point(w, h - 0.5));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var now = DateTime.UtcNow;
        bool isDouble = (now - _lastClick).TotalMilliseconds < 400;
        _lastClick = now;
        if (isDouble)
            EditRequested?.Invoke(this, MCSIndex);
        else
            SelectionRequested?.Invoke(this, MCSIndex);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = !double.IsInfinity(available.Width) ? available.Width : 300;
        return new Size(w, ScreenMetrics.ImplicitButtonHeight + 6);
    }
}