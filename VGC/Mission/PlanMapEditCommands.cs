namespace VGC.Mission;

public abstract record PlanMapEditCommand;

public sealed record MoveMissionWaypointCommand(int Index, PlanCoordinate Coordinate) : PlanMapEditCommand;

public sealed record MoveGeoFencePolygonPointCommand(int PolygonIndex, int PointIndex, PlanCoordinate Coordinate) : PlanMapEditCommand;

public sealed record MoveGeoFenceCircleCommand(int CircleIndex, PlanCoordinate Center) : PlanMapEditCommand;

public sealed record ResizeGeoFenceCircleCommand(int CircleIndex, double Radius) : PlanMapEditCommand;

public sealed record MoveRallyPointCommand(int Index, PlanCoordinate Coordinate) : PlanMapEditCommand;

public sealed record PlanMapEditResult(bool Applied, string? Error = null)
{
    public static PlanMapEditResult Success { get; } = new(true);

    public static PlanMapEditResult Failure(string error)
    {
        return new PlanMapEditResult(false, error);
    }
}
