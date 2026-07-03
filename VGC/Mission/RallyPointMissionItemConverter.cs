using VGC.Mavlink;

namespace VGC.Mission;

public static class RallyPointMissionItemConverter
{
    private const double CoordinateScale = 10000000.0;
    private const byte GlobalRelativeAltFrame = 3;

    public static IReadOnlyList<MavlinkMissionItemInt> ToMissionItems(
        RallyPointsPlan rallyPoints,
        byte targetSystemId,
        byte targetComponentId)
    {
        var items = new List<MavlinkMissionItemInt>();
        for (var i = 0; i < rallyPoints.Points.Count; i++)
        {
            var point = rallyPoints.Points[i];
            items.Add(new MavlinkMissionItemInt(
                TargetSystemId: targetSystemId,
                TargetComponentId: targetComponentId,
                Sequence: checked((ushort)i),
                Command: MavlinkMissionCommandIds.NavRallyPoint,
                Frame: GlobalRelativeAltFrame,
                Current: 0,
                AutoContinue: 0,
                Param1: 0,
                Param2: 0,
                Param3: 0,
                Param4: 0,
                X: ScaleCoordinate(point.Latitude),
                Y: ScaleCoordinate(point.Longitude),
                Z: Convert.ToSingle(point.Altitude.GetValueOrDefault()),
                MissionType: MavMissionType.Rally));
        }

        return items;
    }

    public static RallyPointsPlan ToRallyPointsPlan(IEnumerable<MavlinkMissionItemInt> items)
    {
        var rallyPoints = new RallyPointsPlan();
        foreach (var item in items.OrderBy(static item => item.Sequence))
        {
            if (item.MissionType != MavMissionType.Rally)
            {
                continue;
            }

            if (item.Command != MavlinkMissionCommandIds.NavRallyPoint)
            {
                throw new NotSupportedException($"Rally command {item.Command} is not supported.");
            }

            rallyPoints.Points.Add(new PlanCoordinate(
                item.X / CoordinateScale,
                item.Y / CoordinateScale,
                item.Z));
        }

        return rallyPoints;
    }

    private static int ScaleCoordinate(double value)
    {
        return checked((int)Math.Round(value * CoordinateScale, MidpointRounding.AwayFromZero));
    }
}
