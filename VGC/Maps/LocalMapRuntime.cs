namespace VGC.Maps;

public sealed record MapDisplayPoint(double X, double Y, bool IsVisible);

public sealed record MapDisplayVehicleOverlay(
    byte VehicleId,
    string Label,
    string Mode,
    bool Armed,
    MapDisplayPoint Position);

public sealed record MapDisplayHomeOverlay(
    string Label,
    MapDisplayPoint Position);

public sealed record MapDisplayTrajectoryOverlay(
    byte VehicleId,
    IReadOnlyList<MapDisplayPoint> Points);

public sealed record MapDisplayFrame(
    string ProviderName,
    MapViewport Viewport,
    MapDisplayVehicleOverlay? ActiveVehicle,
    MapDisplayHomeOverlay? Home,
    MapDisplayTrajectoryOverlay? Trajectory)
{
    public bool HasActiveVehicle => ActiveVehicle is not null;
}

public sealed class LocalMapDisplayProjector
{
    private const double DefaultZoomLevel = 16;

    public MapDisplayFrame Project(MapOverlayFrame overlays, MapViewport? viewport = null)
    {
        var effectiveViewport = viewport ?? BuildDefaultViewport(overlays);

        return new MapDisplayFrame(
            "Local Vector",
            effectiveViewport,
            overlays.ActiveVehicle is null
                ? null
                : new MapDisplayVehicleOverlay(
                    overlays.ActiveVehicle.VehicleId,
                    overlays.ActiveVehicle.Label,
                    overlays.ActiveVehicle.Mode,
                    overlays.ActiveVehicle.Armed,
                    ProjectCoordinate(overlays.ActiveVehicle.Coordinate, effectiveViewport)),
            overlays.Home is null
                ? null
                : new MapDisplayHomeOverlay(
                    overlays.Home.Label,
                    ProjectCoordinate(overlays.Home.Coordinate, effectiveViewport)),
            overlays.Trajectory is null
                ? null
                : new MapDisplayTrajectoryOverlay(
                    overlays.Trajectory.VehicleId,
                    overlays.Trajectory.Points.Select(point => ProjectCoordinate(point, effectiveViewport)).ToArray()));
    }

    private static MapViewport BuildDefaultViewport(MapOverlayFrame overlays)
    {
        var center = overlays.ActiveVehicle?.Coordinate
            ?? overlays.Home?.Coordinate
            ?? overlays.Trajectory?.Points.FirstOrDefault()
            ?? new MapCoordinate(0, 0);

        return new MapViewport(center, DefaultZoomLevel);
    }

    public static MapDisplayPoint ProjectCoordinate(MapCoordinate coordinate, MapViewport viewport)
    {
        var zoomScale = Math.Pow(2, Math.Max(0, viewport.ZoomLevel));
        var longitudeSpan = 360 / zoomScale;
        var latitudeSpan = 180 / zoomScale;
        var x = 0.5 + ((coordinate.Longitude - viewport.Center.Longitude) / longitudeSpan);
        var y = 0.5 - ((coordinate.Latitude - viewport.Center.Latitude) / latitudeSpan);
        var visible = x is >= 0 and <= 1 && y is >= 0 and <= 1;

        return new MapDisplayPoint(x, y, visible);
    }
}

public sealed class LocalMapRuntime : IMapProviderAdapter
{
    private readonly LocalMapDisplayProjector _projector = new();
    private MapViewport? _viewport;
    private MapOverlayFrame _overlays = new(null, null, null);

    public LocalMapRuntime()
    {
        DisplayFrame = _projector.Project(_overlays);
    }

    public MapDisplayFrame DisplayFrame { get; private set; }

    public MapProviderDescriptor Descriptor => MapProviderCatalog.LocalFallback;

    public Task ApplyCameraAsync(MapProviderCameraState camera, CancellationToken cancellationToken = default)
    {
        return SetViewportAsync(camera.ToViewport(), cancellationToken);
    }

    public Task<MapProviderCameraState> GetCameraAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MapProviderCameraState.FromViewport(DisplayFrame.Viewport));
    }

    public Task SetViewportAsync(MapViewport viewport, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _viewport = viewport;
        DisplayFrame = _projector.Project(_overlays, _viewport);
        return Task.CompletedTask;
    }

    public Task ApplyOverlaysAsync(MapOverlayFrame overlays, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _overlays = overlays;
        DisplayFrame = _projector.Project(_overlays, _viewport);
        return Task.CompletedTask;
    }
}
