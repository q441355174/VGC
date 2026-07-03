using VGC.Mavlink;

namespace VGC.Mission;

public static class MissionItemConverter
{
    private const double CoordinateScale = 10000000.0;
    private const int RequiredParamCount = 7;

    public static MavlinkMissionItemInt ToMavlinkMissionItemInt(
        MissionPlanItem item,
        byte targetSystemId,
        byte targetComponentId,
        ushort? sequence = null,
        byte current = 0)
    {
        if (!string.Equals(item.Type, "SimpleItem", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Mission item type '{item.Type}' is not supported yet.");
        }

        if (item.Params.Length < RequiredParamCount)
        {
            throw new InvalidOperationException("Mission item params must contain seven values.");
        }

        return new MavlinkMissionItemInt(
            TargetSystemId: targetSystemId,
            TargetComponentId: targetComponentId,
            Sequence: sequence ?? checked((ushort)item.DoJumpId),
            Command: checked((ushort)item.Command),
            Frame: checked((byte)item.Frame),
            Current: current,
            AutoContinue: item.AutoContinue ? (byte)1 : (byte)0,
            Param1: ConvertToSingle(item.Params[0]),
            Param2: ConvertToSingle(item.Params[1]),
            Param3: ConvertToSingle(item.Params[2]),
            Param4: ConvertToSingle(item.Params[3]),
            X: ScaleCoordinate(item.HasCoordinate ? item.Coordinate![1] : item.Params[4]),
            Y: ScaleCoordinate(item.HasCoordinate ? item.Coordinate![0] : item.Params[5]),
            Z: ConvertToSingle(item.HasCoordinate ? item.Coordinate![2] : item.Params[6]));
    }

    public static MissionPlanItem ToMissionPlanItem(MavlinkMissionItemInt item)
    {
        var planItem = new MissionPlanItem
        {
            Type = "SimpleItem",
            Command = item.Command,
            Frame = item.Frame,
            Params =
            [
                item.Param1,
                item.Param2,
                item.Param3,
                item.Param4,
                item.X / CoordinateScale,
                item.Y / CoordinateScale,
                item.Z
            ],
            AutoContinue = item.AutoContinue != 0,
            DoJumpId = item.Sequence
        };
        planItem.SyncCoordinateFromParams();
        return planItem;
    }

    private static float ConvertToSingle(double value)
    {
        return Convert.ToSingle(value);
    }

    private static int ScaleCoordinate(double value)
    {
        return checked((int)Math.Round(value * CoordinateScale, MidpointRounding.AwayFromZero));
    }
}
