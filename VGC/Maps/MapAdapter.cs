using VGC.Vehicles;

namespace VGC.Maps;

public sealed record MapCoordinate(double Latitude, double Longitude, double? AltitudeMeters = null)
{
    public static MapCoordinate FromVehicleCoordinate(VehicleCoordinate coordinate)
    {
        return new MapCoordinate(coordinate.Latitude, coordinate.Longitude, coordinate.AltitudeMeters);
    }
}

public sealed record MapViewport(MapCoordinate Center, double ZoomLevel);

public sealed record VehicleMapOverlay(
    byte VehicleId,
    MapCoordinate Coordinate,
    string Mode,
    bool Armed,
    string Label);

public sealed record HomeMapOverlay(MapCoordinate Coordinate, string Label = "Home");

public sealed record TrajectoryMapOverlay(byte VehicleId, IReadOnlyList<MapCoordinate> Points);

public sealed record MapOverlayFrame(
    VehicleMapOverlay? ActiveVehicle,
    HomeMapOverlay? Home,
    TrajectoryMapOverlay? Trajectory);

public interface IMapAdapter
{
    Task SetViewportAsync(MapViewport viewport, CancellationToken cancellationToken = default);

    Task ApplyOverlaysAsync(MapOverlayFrame overlays, CancellationToken cancellationToken = default);
}
