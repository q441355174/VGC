namespace VGC.Mavlink;

public static class MavlinkCommandIds
{
    public const ushort NavReturnToLaunch = 20;
    public const ushort NavLand = 21;
    public const ushort NavTakeoff = 22;
    public const ushort DoSetMode = 176;
    public const ushort DoPauseContinue = 193;
    public const ushort ComponentArmDisarm = 400;
    public const ushort ImageStartCapture = 2000;
    public const ushort ImageStopCapture = 2001;
    public const ushort VideoStartCapture = 2500;
    public const ushort VideoStopCapture = 2501;
    public const ushort RequestCameraInformation = 521;
}

public enum MavlinkCommandResult : byte
{
    Accepted = 0,
    TemporarilyRejected = 1,
    Denied = 2,
    Unsupported = 3,
    Failed = 4,
    InProgress = 5,
    Cancelled = 6,
    CommandLongOnly = 7,
    CommandIntOnly = 8,
    CommandUnsupportedMavFrame = 9
}

public sealed record MavlinkCommandLong(
    byte TargetSystemId,
    byte TargetComponentId,
    ushort Command,
    byte Confirmation = 0,
    float Param1 = 0,
    float Param2 = 0,
    float Param3 = 0,
    float Param4 = 0,
    float Param5 = 0,
    float Param6 = 0,
    float Param7 = 0);

public sealed record MavlinkCommandAck(
    byte SystemId,
    byte ComponentId,
    ushort Command,
    MavlinkCommandResult Result);
