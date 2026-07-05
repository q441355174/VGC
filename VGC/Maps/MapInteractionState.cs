namespace VGC.Maps;

public sealed class MapInteractionState
{
    private const double FollowZoomLevel = 16;
    private const double DefaultZoomLevel = 12;
    private static readonly MapCoordinate DefaultCenter = new(39.9042, 116.4074);
    private MapViewport? _manualViewport;

    public bool IsFollowingVehicle { get; private set; } = true;

    public MapViewport ResolveViewport(MapCoordinate? activeVehicleCoordinate)
    {
        if (IsFollowingVehicle && activeVehicleCoordinate is not null)
        {
            return new MapViewport(activeVehicleCoordinate, _manualViewport?.ZoomLevel ?? FollowZoomLevel);
        }

        return _manualViewport
            ?? new MapViewport(activeVehicleCoordinate ?? DefaultCenter, DefaultZoomLevel);
    }

    public void MarkManualViewport(MapViewport viewport)
    {
        _manualViewport = viewport;
        IsFollowingVehicle = false;
    }

    public void RecenterOnVehicle()
    {
        IsFollowingVehicle = true;
        _manualViewport = null;
    }
}
