using VGC.Mission;

namespace VGC.Maps;

public enum MapProviderOverlayLayer
{
    Vehicle,
    Home,
    Trajectory,
    Mission,
    GeoFence,
    Rally,
    Traffic,
    RemoteId
}

public sealed record MapProviderOverlayStyle(
    string StrokeColor,
    string? FillColor = null,
    double StrokeWidth = 2);

public abstract record MapProviderOverlayCommand(
    string Id,
    MapProviderOverlayLayer Layer,
    string Label,
    int ZIndex);

public sealed record MapProviderMarkerCommand(
    string Id,
    MapProviderOverlayLayer Layer,
    string Label,
    int ZIndex,
    MapCoordinate Coordinate,
    string Symbol)
    : MapProviderOverlayCommand(Id, Layer, Label, ZIndex);

public sealed record MapProviderPolylineCommand(
    string Id,
    MapProviderOverlayLayer Layer,
    string Label,
    int ZIndex,
    IReadOnlyList<MapCoordinate> Points,
    MapProviderOverlayStyle Style)
    : MapProviderOverlayCommand(Id, Layer, Label, ZIndex);

public sealed record MapProviderPolygonCommand(
    string Id,
    MapProviderOverlayLayer Layer,
    string Label,
    int ZIndex,
    IReadOnlyList<MapCoordinate> Points,
    MapProviderOverlayStyle Style,
    bool Inclusion)
    : MapProviderOverlayCommand(Id, Layer, Label, ZIndex);

public sealed record MapProviderCircleCommand(
    string Id,
    MapProviderOverlayLayer Layer,
    string Label,
    int ZIndex,
    MapCoordinate Center,
    double RadiusMeters,
    MapProviderOverlayStyle Style,
    bool Inclusion)
    : MapProviderOverlayCommand(Id, Layer, Label, ZIndex);

public sealed record MapProviderOverlayCommandFrame(IReadOnlyList<MapProviderOverlayCommand> Commands)
{
    public IReadOnlyList<MapProviderMarkerCommand> Markers =>
        Commands.OfType<MapProviderMarkerCommand>().ToArray();

    public IReadOnlyList<MapProviderPolylineCommand> Polylines =>
        Commands.OfType<MapProviderPolylineCommand>().ToArray();

    public IReadOnlyList<MapProviderPolygonCommand> Polygons =>
        Commands.OfType<MapProviderPolygonCommand>().ToArray();

    public IReadOnlyList<MapProviderCircleCommand> Circles =>
        Commands.OfType<MapProviderCircleCommand>().ToArray();
}

public sealed class MapProviderOverlayBridge
{
    public MapProviderOverlayCommandFrame Build(MapOverlayFrame vehicleOverlays, PlanMapOverlay? planOverlay = null)
    {
        var commands = new List<MapProviderOverlayCommand>();

        AddVehicleOverlays(vehicleOverlays, commands);

        if (planOverlay is not null)
        {
            AddPlanOverlays(planOverlay, commands);
        }

        return new MapProviderOverlayCommandFrame(commands);
    }

    private static void AddVehicleOverlays(MapOverlayFrame overlays, List<MapProviderOverlayCommand> commands)
    {
        if (overlays.Trajectory is { Points.Count: > 1 } trajectory)
        {
            commands.Add(new MapProviderPolylineCommand(
                $"trajectory:{trajectory.VehicleId}",
                MapProviderOverlayLayer.Trajectory,
                $"Vehicle {trajectory.VehicleId} track",
                20,
                trajectory.Points,
                new MapProviderOverlayStyle("#54a8ff", StrokeWidth: 3)));
        }

        if (overlays.Home is { } home)
        {
            commands.Add(new MapProviderMarkerCommand(
                "home",
                MapProviderOverlayLayer.Home,
                home.Label,
                40,
                home.Coordinate,
                "home"));
        }

        if (overlays.ActiveVehicle is { } vehicle)
        {
            commands.Add(new MapProviderMarkerCommand(
                $"vehicle:{vehicle.VehicleId}",
                MapProviderOverlayLayer.Vehicle,
                vehicle.Label,
                50,
                vehicle.Coordinate,
                vehicle.Armed ? "vehicle-armed" : "vehicle-disarmed"));
        }
    }

    private static void AddPlanOverlays(PlanMapOverlay overlay, List<MapProviderOverlayCommand> commands)
    {
        foreach (var waypoint in overlay.MissionWaypoints)
        {
            commands.Add(new MapProviderMarkerCommand(
                $"mission:waypoint:{waypoint.Sequence}",
                MapProviderOverlayLayer.Mission,
                $"Waypoint {waypoint.Sequence}",
                30,
                ToMapCoordinate(waypoint.Coordinate),
                "mission-waypoint"));
        }

        foreach (var polygon in overlay.GeoFencePolygons)
        {
            commands.Add(new MapProviderPolygonCommand(
                $"geofence:polygon:{polygon.Index}",
                MapProviderOverlayLayer.GeoFence,
                polygon.Inclusion ? $"GeoFence inclusion {polygon.Index}" : $"GeoFence exclusion {polygon.Index}",
                25,
                polygon.Points.Select(ToMapCoordinate).ToArray(),
                polygon.Inclusion
                    ? new MapProviderOverlayStyle("#3ac779", "#3ac77933", StrokeWidth: 2)
                    : new MapProviderOverlayStyle("#ff6b6b", "#ff6b6b33", StrokeWidth: 2),
                polygon.Inclusion));
        }

        foreach (var circle in overlay.GeoFenceCircles)
        {
            commands.Add(new MapProviderCircleCommand(
                $"geofence:circle:{circle.Index}",
                MapProviderOverlayLayer.GeoFence,
                circle.Inclusion ? $"GeoFence inclusion circle {circle.Index}" : $"GeoFence exclusion circle {circle.Index}",
                25,
                ToMapCoordinate(circle.Center),
                circle.Radius,
                circle.Inclusion
                    ? new MapProviderOverlayStyle("#3ac779", "#3ac77933", StrokeWidth: 2)
                    : new MapProviderOverlayStyle("#ff6b6b", "#ff6b6b33", StrokeWidth: 2),
                circle.Inclusion));
        }

        foreach (var rallyPoint in overlay.RallyPoints)
        {
            commands.Add(new MapProviderMarkerCommand(
                $"rally:{rallyPoint.Index}",
                MapProviderOverlayLayer.Rally,
                $"Rally {rallyPoint.Index}",
                35,
                ToMapCoordinate(rallyPoint.Coordinate),
                "rally-point"));
        }
    }

    private static MapCoordinate ToMapCoordinate(PlanCoordinate coordinate)
    {
        return new MapCoordinate(coordinate.Latitude, coordinate.Longitude, coordinate.Altitude);
    }
}
