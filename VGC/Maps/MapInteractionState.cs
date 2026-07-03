namespace VGC.Maps;

public sealed class MapInteractionState
{
    private const double DefaultZoomLevel = 16;
    private MapViewport? _manualViewport;

    public bool IsFollowingVehicle { get; private set; } = true;

    public MapViewport ResolveViewport(MapCoordinate? activeVehicleCoordinate)
    {
        if (IsFollowingVehicle && activeVehicleCoordinate is not null)
        {
            return new MapViewport(activeVehicleCoordinate, _manualViewport?.ZoomLevel ?? DefaultZoomLevel);
        }

        return _manualViewport
            ?? new MapViewport(activeVehicleCoordinate ?? new MapCoordinate(0, 0), DefaultZoomLevel);
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
