namespace VGC.Mission;

public sealed class PlanMapOverlay
{
    public IReadOnlyList<PlanMapWaypointOverlay> MissionWaypoints { get; init; } = [];

    public IReadOnlyList<PlanMapPolygonOverlay> GeoFencePolygons { get; init; } = [];

    public IReadOnlyList<PlanMapCircleOverlay> GeoFenceCircles { get; init; } = [];

    public IReadOnlyList<PlanMapPointOverlay> RallyPoints { get; init; } = [];
}

public sealed record PlanMapWaypointOverlay(int Sequence, PlanCoordinate Coordinate, int Command);

public sealed record PlanMapPolygonOverlay(int Index, IReadOnlyList<PlanCoordinate> Points, bool Inclusion);

public sealed record PlanMapCircleOverlay(int Index, PlanCoordinate Center, double Radius, bool Inclusion);

public sealed record PlanMapPointOverlay(int Index, PlanCoordinate Coordinate);
