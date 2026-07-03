using VGC.Mission;

namespace VGC.Maps;

public sealed record PlanWaypointMapDisplayOverlay(
    int Sequence,
    int Command,
    MapDisplayPoint Position);

public sealed record PlanPolygonMapDisplayOverlay(
    int Index,
    IReadOnlyList<MapDisplayPoint> Points,
    bool Inclusion);

public sealed record PlanCircleMapDisplayOverlay(
    int Index,
    MapDisplayPoint Center,
    double RadiusMeters,
    bool Inclusion);

public sealed record PlanRallyPointMapDisplayOverlay(
    int Index,
    MapDisplayPoint Position);

public sealed record PlanMapDisplayFrame(
    MapViewport Viewport,
    IReadOnlyList<PlanWaypointMapDisplayOverlay> MissionWaypoints,
    IReadOnlyList<PlanPolygonMapDisplayOverlay> GeoFencePolygons,
    IReadOnlyList<PlanCircleMapDisplayOverlay> GeoFenceCircles,
    IReadOnlyList<PlanRallyPointMapDisplayOverlay> RallyPoints)
{
    public bool HasAnyOverlay =>
        MissionWaypoints.Count > 0
        || GeoFencePolygons.Count > 0
        || GeoFenceCircles.Count > 0
        || RallyPoints.Count > 0;
}

public sealed class PlanMapDisplayProjector
{
    private const double DefaultZoomLevel = 16;

    public PlanMapDisplayFrame Project(PlanMapOverlay overlay, MapViewport? viewport = null)
    {
        var effectiveViewport = viewport ?? BuildDefaultViewport(overlay);

        return new PlanMapDisplayFrame(
            effectiveViewport,
            overlay.MissionWaypoints
                .Select(waypoint => new PlanWaypointMapDisplayOverlay(
                    waypoint.Sequence,
                    waypoint.Command,
                    Project(waypoint.Coordinate, effectiveViewport)))
                .ToArray(),
            overlay.GeoFencePolygons
                .Select(polygon => new PlanPolygonMapDisplayOverlay(
                    polygon.Index,
                    polygon.Points.Select(point => Project(point, effectiveViewport)).ToArray(),
                    polygon.Inclusion))
                .ToArray(),
            overlay.GeoFenceCircles
                .Select(circle => new PlanCircleMapDisplayOverlay(
                    circle.Index,
                    Project(circle.Center, effectiveViewport),
                    circle.Radius,
                    circle.Inclusion))
                .ToArray(),
            overlay.RallyPoints
                .Select(point => new PlanRallyPointMapDisplayOverlay(
                    point.Index,
                    Project(point.Coordinate, effectiveViewport)))
                .ToArray());
    }

    private static MapViewport BuildDefaultViewport(PlanMapOverlay overlay)
    {
        var center = EnumerateCoordinates(overlay).FirstOrDefault() ?? new MapCoordinate(0, 0);
        return new MapViewport(center, DefaultZoomLevel);
    }

    private static IEnumerable<MapCoordinate> EnumerateCoordinates(PlanMapOverlay overlay)
    {
        foreach (var waypoint in overlay.MissionWaypoints)
        {
            yield return ToMapCoordinate(waypoint.Coordinate);
        }

        foreach (var polygon in overlay.GeoFencePolygons)
        {
            foreach (var point in polygon.Points)
            {
                yield return ToMapCoordinate(point);
            }
        }

        foreach (var circle in overlay.GeoFenceCircles)
        {
            yield return ToMapCoordinate(circle.Center);
        }

        foreach (var rallyPoint in overlay.RallyPoints)
        {
            yield return ToMapCoordinate(rallyPoint.Coordinate);
        }
    }

    private static MapDisplayPoint Project(PlanCoordinate coordinate, MapViewport viewport)
    {
        return LocalMapDisplayProjector.ProjectCoordinate(ToMapCoordinate(coordinate), viewport);
    }

    private static MapCoordinate ToMapCoordinate(PlanCoordinate coordinate)
    {
        return new MapCoordinate(coordinate.Latitude, coordinate.Longitude, coordinate.Altitude);
    }
}
