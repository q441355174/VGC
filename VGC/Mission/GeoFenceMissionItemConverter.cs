using VGC.Mavlink;

namespace VGC.Mission;

public static class GeoFenceMissionItemConverter
{
    private const double CoordinateScale = 10000000.0;
    private const byte GlobalFrame = 0;
    private const byte GlobalRelativeAltFrame = 3;

    public static IReadOnlyList<MavlinkMissionItemInt> ToMissionItems(
        GeoFencePlan geoFence,
        byte targetSystemId,
        byte targetComponentId)
    {
        var items = new List<MavlinkMissionItemInt>();
        ushort sequence = 0;

        foreach (var polygon in geoFence.Polygons)
        {
            var command = polygon.Inclusion
                ? MavlinkMissionCommandIds.NavFencePolygonVertexInclusion
                : MavlinkMissionCommandIds.NavFencePolygonVertexExclusion;
            var vertexCount = Convert.ToSingle(polygon.Polygon.Count);

            foreach (var vertex in polygon.Polygon)
            {
                items.Add(CreateFenceItem(
                    targetSystemId,
                    targetComponentId,
                    sequence++,
                    command,
                    GlobalFrame,
                    param1: vertexCount,
                    coordinate: vertex,
                    altitude: 0));
            }
        }

        foreach (var circle in geoFence.Circles)
        {
            var command = circle.Inclusion
                ? MavlinkMissionCommandIds.NavFenceCircleInclusion
                : MavlinkMissionCommandIds.NavFenceCircleExclusion;

            items.Add(CreateFenceItem(
                targetSystemId,
                targetComponentId,
                sequence++,
                command,
                GlobalFrame,
                param1: Convert.ToSingle(circle.Circle.Radius),
                coordinate: circle.Circle.Center,
                altitude: 0));
        }

        if (geoFence.BreachReturn is not null)
        {
            items.Add(CreateFenceItem(
                targetSystemId,
                targetComponentId,
                sequence++,
                MavlinkMissionCommandIds.NavFenceReturnPoint,
                GlobalRelativeAltFrame,
                param1: 0,
                coordinate: geoFence.BreachReturn,
                altitude: Convert.ToSingle(geoFence.BreachReturn.Altitude.GetValueOrDefault())));
        }

        return items;
    }

    public static GeoFencePlan ToGeoFencePlan(IEnumerable<MavlinkMissionItemInt> items)
    {
        var geoFence = new GeoFencePlan();
        GeoFencePolygon? polygon = null;
        ushort expectedPolygonCommand = 0;
        var expectedVertexCount = 0;

        foreach (var item in items.OrderBy(static item => item.Sequence))
        {
            if (item.MissionType != MavMissionType.Fence)
            {
                continue;
            }

            switch (item.Command)
            {
                case MavlinkMissionCommandIds.NavFencePolygonVertexInclusion:
                case MavlinkMissionCommandIds.NavFencePolygonVertexExclusion:
                    var vertexCount = Convert.ToInt32(item.Param1);
                    if (polygon is null)
                    {
                        polygon = new GeoFencePolygon
                        {
                            Inclusion = item.Command == MavlinkMissionCommandIds.NavFencePolygonVertexInclusion
                        };
                        expectedPolygonCommand = item.Command;
                        expectedVertexCount = vertexCount;
                    }
                    else if (expectedPolygonCommand != item.Command || expectedVertexCount != vertexCount)
                    {
                        throw new InvalidOperationException("GeoFence polygon item sequence changed before polygon completion.");
                    }

                    polygon.Polygon.Add(ToCoordinate(item));
                    if (polygon.Polygon.Count == expectedVertexCount)
                    {
                        geoFence.Polygons.Add(polygon);
                        polygon = null;
                        expectedPolygonCommand = 0;
                        expectedVertexCount = 0;
                    }

                    break;
                case MavlinkMissionCommandIds.NavFenceCircleInclusion:
                case MavlinkMissionCommandIds.NavFenceCircleExclusion:
                    if (polygon is not null)
                    {
                        throw new InvalidOperationException("GeoFence circle item encountered before polygon completion.");
                    }

                    geoFence.Circles.Add(new GeoFenceCircle
                    {
                        Inclusion = item.Command == MavlinkMissionCommandIds.NavFenceCircleInclusion,
                        Circle = new GeoFenceCircleShape
                        {
                            Center = ToCoordinate(item),
                            Radius = item.Param1
                        }
                    });
                    break;
                case MavlinkMissionCommandIds.NavFenceReturnPoint:
                    geoFence.BreachReturn = ToCoordinate(item, item.Z);
                    break;
                default:
                    throw new NotSupportedException($"GeoFence command {item.Command} is not supported.");
            }
        }

        if (polygon is not null)
        {
            throw new InvalidOperationException("GeoFence polygon item sequence ended before polygon completion.");
        }

        return geoFence;
    }

    private static MavlinkMissionItemInt CreateFenceItem(
        byte targetSystemId,
        byte targetComponentId,
        ushort sequence,
        ushort command,
        byte frame,
        float param1,
        PlanCoordinate coordinate,
        float altitude)
    {
        return new MavlinkMissionItemInt(
            TargetSystemId: targetSystemId,
            TargetComponentId: targetComponentId,
            Sequence: sequence,
            Command: command,
            Frame: frame,
            Current: 0,
            AutoContinue: 0,
            Param1: param1,
            Param2: 0,
            Param3: 0,
            Param4: 0,
            X: ScaleCoordinate(coordinate.Latitude),
            Y: ScaleCoordinate(coordinate.Longitude),
            Z: altitude,
            MissionType: MavMissionType.Fence);
    }

    private static PlanCoordinate ToCoordinate(MavlinkMissionItemInt item)
    {
        return new PlanCoordinate(item.X / CoordinateScale, item.Y / CoordinateScale);
    }

    private static PlanCoordinate ToCoordinate(MavlinkMissionItemInt item, double altitude)
    {
        return new PlanCoordinate(item.X / CoordinateScale, item.Y / CoordinateScale, altitude);
    }

    private static int ScaleCoordinate(double value)
    {
        return checked((int)Math.Round(value * CoordinateScale, MidpointRounding.AwayFromZero));
    }
}
