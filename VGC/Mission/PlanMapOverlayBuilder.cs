namespace VGC.Mission;

public sealed class PlanMapOverlayBuilder
{
    public PlanMapOverlay Build(PlanSectionCoordinator coordinator)
    {
        return new PlanMapOverlay
        {
            MissionWaypoints = BuildMissionWaypoints(coordinator.Mission),
            GeoFencePolygons = BuildGeoFencePolygons(coordinator.GeoFence),
            GeoFenceCircles = BuildGeoFenceCircles(coordinator.GeoFence),
            RallyPoints = BuildRallyPoints(coordinator.RallyPoints)
        };
    }

    private static IReadOnlyList<PlanMapWaypointOverlay> BuildMissionWaypoints(MissionPlan mission)
    {
        var overlays = new List<PlanMapWaypointOverlay>();
        for (var index = 0; index < mission.Items.Count; index++)
        {
            var item = mission.Items[index];
            if (item.Params.Length < 7)
            {
                continue;
            }

            var coordinate = new PlanCoordinate(item.Params[4], item.Params[5], item.Params[6]);
            if (!coordinate.IsValid3D())
            {
                continue;
            }

            overlays.Add(new PlanMapWaypointOverlay(index, coordinate, item.Command));
        }

        return overlays;
    }

    private static IReadOnlyList<PlanMapPolygonOverlay> BuildGeoFencePolygons(GeoFencePlan geoFence)
    {
        var overlays = new List<PlanMapPolygonOverlay>();
        for (var index = 0; index < geoFence.Polygons.Count; index++)
        {
            var polygon = geoFence.Polygons[index];
            overlays.Add(new PlanMapPolygonOverlay(index, polygon.Polygon.ToArray(), polygon.Inclusion));
        }

        return overlays;
    }

    private static IReadOnlyList<PlanMapCircleOverlay> BuildGeoFenceCircles(GeoFencePlan geoFence)
    {
        var overlays = new List<PlanMapCircleOverlay>();
        for (var index = 0; index < geoFence.Circles.Count; index++)
        {
            var circle = geoFence.Circles[index];
            overlays.Add(new PlanMapCircleOverlay(index, circle.Circle.Center, circle.Circle.Radius, circle.Inclusion));
        }

        return overlays;
    }

    private static IReadOnlyList<PlanMapPointOverlay> BuildRallyPoints(RallyPointsPlan rallyPoints)
    {
        var overlays = new List<PlanMapPointOverlay>();
        for (var index = 0; index < rallyPoints.Points.Count; index++)
        {
            overlays.Add(new PlanMapPointOverlay(index, rallyPoints.Points[index]));
        }

        return overlays;
    }
}
