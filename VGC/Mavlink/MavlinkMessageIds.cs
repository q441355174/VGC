namespace VGC.Mavlink;

public static class MavlinkMessageIds
{
    public const uint Heartbeat = 0;
    public const uint SysStatus = 1;
    public const uint ParamRequestRead = 20;
    public const uint ParamRequestList = 21;
    public const uint ParamValue = 22;
    public const uint ParamSet = 23;
    public const uint Attitude = 30;
    public const uint GlobalPositionInt = 33;
    public const uint MissionRequestList = 43;
    public const uint MissionCount = 44;
    public const uint MissionAck = 47;
    public const uint MissionRequestInt = 51;
    public const uint MissionItemInt = 73;
    public const uint CommandLong = 76;
    public const uint CommandAck = 77;
    public const uint FileTransferProtocol = 110;
    public const uint Statustext = 253;
}
