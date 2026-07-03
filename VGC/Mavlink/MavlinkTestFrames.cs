using VGC.Vehicles;

namespace VGC.Mavlink;

public static class MavlinkTestFrames
{
    private static readonly MavlinkFrameWriter FrameWriter = new();

    public static byte[] Heartbeat(byte systemId = 1, byte componentId = 1, MavAutopilot autopilot = MavAutopilot.Px4, MavType vehicleType = MavType.Quadrotor)
    {
        return HeartbeatV1(systemId, componentId, autopilot, vehicleType);
    }

    public static byte[] HeartbeatV1(byte systemId = 1, byte componentId = 1, MavAutopilot autopilot = MavAutopilot.Px4, MavType vehicleType = MavType.Quadrotor, byte baseMode = 0, uint customMode = 0)
    {
        var payload = new byte[9];
        BitConverter.GetBytes(customMode).CopyTo(payload, 0);
        payload[4] = (byte)vehicleType;
        payload[5] = (byte)autopilot;
        payload[6] = baseMode;
        payload[7] = 4;
        payload[8] = 3;

        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: 0, payload, crcExtra: 50);
    }

    public static byte[] HeartbeatV2(byte systemId = 1, byte componentId = 1, MavAutopilot autopilot = MavAutopilot.Px4, MavType vehicleType = MavType.Quadrotor)
    {
        var payload = new byte[9];
        payload[4] = (byte)vehicleType;
        payload[5] = (byte)autopilot;
        payload[6] = 0;
        payload[7] = 4;
        payload[8] = 3;

        return
        [
            0xFD,
            0x09,
            0x00,
            0x00,
            0x00,
            systemId,
            componentId,
            0x00,
            0x00,
            0x00,
            payload[0],
            payload[1],
            payload[2],
            payload[3],
            payload[4],
            payload[5],
            payload[6],
            payload[7],
            payload[8],
            0x00,
            0x00
        ];
    }

    public static byte[] GcsHeartbeat()
    {
        return FrameWriter.CreateGcsHeartbeat();
    }

    public static byte[] GlobalPositionInt(byte systemId = 1, byte componentId = 1, double latitude = 47.397742, double longitude = 8.545594, double altitudeMeters = 488.0, double relativeAltitudeMeters = 12.3)
    {
        var payload = new byte[28];
        BitConverter.GetBytes((uint)1000).CopyTo(payload, 0);
        BitConverter.GetBytes((int)(latitude * 10000000)).CopyTo(payload, 4);
        BitConverter.GetBytes((int)(longitude * 10000000)).CopyTo(payload, 8);
        BitConverter.GetBytes((int)(altitudeMeters * 1000)).CopyTo(payload, 12);
        BitConverter.GetBytes((int)(relativeAltitudeMeters * 1000)).CopyTo(payload, 16);
        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: 33, payload, crcExtra: 104);
    }

    public static byte[] SysStatus(byte systemId = 1, byte componentId = 1, ushort voltageMillivolts = 12000, sbyte batteryRemaining = 87)
    {
        var payload = new byte[31];
        BitConverter.GetBytes(voltageMillivolts).CopyTo(payload, 14);
        payload[30] = unchecked((byte)batteryRemaining);
        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: 1, payload, crcExtra: 124);
    }

    public static byte[] GpsRawInt(byte systemId = 1, byte componentId = 1, byte fixType = 3, byte satellitesVisible = 14)
    {
        var payload = new byte[30];
        payload[8] = fixType;
        payload[29] = satellitesVisible;
        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: 24, payload, crcExtra: 24);
    }

    public static byte[] ParamValue(byte systemId = 1, byte componentId = 1, string name = "SYS_ID", float value = 1, ushort count = 1, ushort index = 0, byte mavParamType = 9)
    {
        var payload = new byte[25];
        BitConverter.GetBytes(value).CopyTo(payload, 0);
        BitConverter.GetBytes(count).CopyTo(payload, 4);
        BitConverter.GetBytes(index).CopyTo(payload, 6);
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, payload, 8, Math.Min(nameBytes.Length, 16));
        payload[24] = mavParamType;
        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: 22, payload, crcExtra: 220);
    }

    public static byte[] CommandAck(byte systemId = 1, byte componentId = 1, ushort command = MavlinkCommandIds.ComponentArmDisarm, MavlinkCommandResult result = MavlinkCommandResult.Accepted)
    {
        var payload = new byte[3];
        BitConverter.GetBytes(command).CopyTo(payload, 0);
        payload[2] = (byte)result;
        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: 77, payload, crcExtra: 143);
    }

    public static byte[] StatusText(byte systemId = 1, byte componentId = 1, MavlinkSeverity severity = MavlinkSeverity.Warning, string text = "EKF variance")
    {
        var payload = new byte[51];
        payload[0] = (byte)severity;
        var textBytes = System.Text.Encoding.ASCII.GetBytes(text);
        Array.Copy(textBytes, 0, payload, 1, Math.Min(textBytes.Length, 50));
        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: (byte)MavlinkMessageIds.Statustext, payload, crcExtra: 83);
    }

    public static byte[] Attitude(byte systemId = 1, byte componentId = 1, float roll = 0.1f, float pitch = 0.2f, float yaw = 1.5f)
    {
        var payload = new byte[28];
        Array.Copy(BitConverter.GetBytes(roll), 0, payload, 0, 4);
        Array.Copy(BitConverter.GetBytes(pitch), 0, payload, 4, 4);
        Array.Copy(BitConverter.GetBytes(yaw), 0, payload, 8, 4);
        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: (byte)MavlinkMessageIds.Attitude, payload, crcExtra: 39);
    }

    public static byte[] EstimatorStatus(byte systemId = 1, byte componentId = 1, ushort flags = 1)
    {
        var payload = new byte[42];
        BitConverter.GetBytes(flags).CopyTo(payload, 8);
        return FrameWriter.CreateV2Frame(systemId, componentId, messageId: 256, payload, crcExtra: 159);
    }

    public static byte[] HomePosition(byte systemId = 1, byte componentId = 1, double lat = 47.397742, double lon = 8.545594, double alt = 500)
    {
        var payload = new byte[28];
        Array.Copy(BitConverter.GetBytes((int)(lat * 10000000)), 0, payload, 0, 4);
        Array.Copy(BitConverter.GetBytes((int)(lon * 10000000)), 0, payload, 4, 4);
        Array.Copy(BitConverter.GetBytes((int)(alt * 1000)), 0, payload, 8, 4);
        return FrameWriter.CreateV1Frame(systemId, componentId, messageId: 110, payload, crcExtra: 106);
    }
}
