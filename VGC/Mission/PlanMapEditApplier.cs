namespace VGC.Mission;

public sealed class PlanMapEditApplier
{
    public PlanMapEditResult Apply(PlanSectionCoordinator coordinator, PlanMapEditCommand command)
    {
        return command switch
        {
            MoveMissionWaypointCommand moveMissionWaypoint => ApplyMissionWaypoint(coordinator.Mission, moveMissionWaypoint),
            MoveGeoFencePolygonPointCommand moveGeoFencePolygonPoint => ApplyGeoFencePolygonPoint(coordinator.GeoFence, moveGeoFencePolygonPoint),
            MoveGeoFenceCircleCommand moveGeoFenceCircle => ApplyGeoFenceCircle(coordinator.GeoFence, moveGeoFenceCircle),
            ResizeGeoFenceCircleCommand resizeGeoFenceCircle => ApplyGeoFenceCircleRadius(coordinator.GeoFence, resizeGeoFenceCircle),
            MoveRallyPointCommand moveRallyPoint => ApplyRallyPoint(coordinator.RallyPoints, moveRallyPoint),
            _ => PlanMapEditResult.Failure("Unsupported plan map edit command.")
        };
    }

    private static PlanMapEditResult ApplyMissionWaypoint(MissionPlan mission, MoveMissionWaypointCommand command)
    {
        if (command.Index < 0 || command.Index >= mission.Items.Count)
        {
            return PlanMapEditResult.Failure("Mission waypoint index is out of range.");
        }

        if (!command.Coordinate.IsValid3D())
        {
            return PlanMapEditResult.Failure("Mission waypoint coordinate is invalid.");
        }

        var item = mission.Items[command.Index];
        if (item.Params.Length < 7)
        {
            var expanded = new double[7];
            Array.Copy(item.Params, expanded, item.Params.Length);
            item.Params = expanded;
        }

        item.Params[4] = command.Coordinate.Latitude;
        item.Params[5] = command.Coordinate.Longitude;
        item.Params[6] = command.Coordinate.Altitude.GetValueOrDefault();
        return PlanMapEditResult.Success;
    }

    private static PlanMapEditResult ApplyGeoFencePolygonPoint(GeoFencePlan geoFence, MoveGeoFencePolygonPointCommand command)
    {
        if (command.PolygonIndex < 0 || command.PolygonIndex >= geoFence.Polygons.Count)
        {
            return PlanMapEditResult.Failure("GeoFence polygon index is out of range.");
        }

        var polygon = geoFence.Polygons[command.PolygonIndex];
        if (command.PointIndex < 0 || command.PointIndex >= polygon.Polygon.Count)
        {
            return PlanMapEditResult.Failure("GeoFence polygon point index is out of range.");
        }

        if (!command.Coordinate.IsValid2D())
        {
            return PlanMapEditResult.Failure("GeoFence polygon coordinate is invalid.");
        }

        polygon.Polygon[command.PointIndex] = new PlanCoordinate(command.Coordinate.Latitude, command.Coordinate.Longitude);
        return ValidateGeoFence(geoFence);
    }

    private static PlanMapEditResult ApplyGeoFenceCircle(GeoFencePlan geoFence, MoveGeoFenceCircleCommand command)
    {
        if (command.CircleIndex < 0 || command.CircleIndex >= geoFence.Circles.Count)
        {
            return PlanMapEditResult.Failure("GeoFence circle index is out of range.");
        }

        if (!command.Center.IsValid2D())
        {
            return PlanMapEditResult.Failure("GeoFence circle center is invalid.");
        }

        geoFence.Circles[command.CircleIndex].Circle.Center = new PlanCoordinate(command.Center.Latitude, command.Center.Longitude);
        return ValidateGeoFence(geoFence);
    }

    private static PlanMapEditResult ApplyGeoFenceCircleRadius(GeoFencePlan geoFence, ResizeGeoFenceCircleCommand command)
    {
        if (command.CircleIndex < 0 || command.CircleIndex >= geoFence.Circles.Count)
        {
            return PlanMapEditResult.Failure("GeoFence circle index is out of range.");
        }

        geoFence.Circles[command.CircleIndex].Circle.Radius = command.Radius;
        return ValidateGeoFence(geoFence);
    }

    private static PlanMapEditResult ApplyRallyPoint(RallyPointsPlan rallyPoints, MoveRallyPointCommand command)
    {
        if (command.Index < 0 || command.Index >= rallyPoints.Points.Count)
        {
            return PlanMapEditResult.Failure("Rally point index is out of range.");
        }

        if (!command.Coordinate.IsValid3D())
        {
            return PlanMapEditResult.Failure("Rally point coordinate is invalid.");
        }

        rallyPoints.Points[command.Index] = command.Coordinate;
        return ValidateRally(rallyPoints);
    }

    private static PlanMapEditResult ValidateGeoFence(GeoFencePlan geoFence)
    {
        var validation = GeoFenceValidation.Validate(geoFence);
        return validation.IsValid
            ? PlanMapEditResult.Success
            : PlanMapEditResult.Failure(string.Join(" | ", validation.Errors));
    }

    private static PlanMapEditResult ValidateRally(RallyPointsPlan rallyPoints)
    {
        var validation = RallyPointsValidation.Validate(rallyPoints);
        return validation.IsValid
            ? PlanMapEditResult.Success
            : PlanMapEditResult.Failure(string.Join(" | ", validation.Errors));
    }
}
