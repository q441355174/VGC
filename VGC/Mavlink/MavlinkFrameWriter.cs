using VGC.Vehicles;
using System.Text;

namespace VGC.Mavlink;

public sealed class MavlinkFrameWriter
{
    private byte _sequence;

    public byte[] CreateGcsHeartbeat(byte systemId = 255, byte componentId = 190)
    {
        var payload = new byte[9];
        payload[4] = (byte)MavType.Gcs;
        payload[5] = (byte)MavAutopilot.Invalid;
        payload[6] = 0;
        payload[7] = 4;
        payload[8] = 3;

        return CreateV1Frame(systemId, componentId, messageId: 0, payload, crcExtra: 50);
    }

    public byte[] CreateCommandLong(byte systemId, byte componentId, MavlinkCommandLong command)
    {
        var payload = new byte[33];
        WriteSingle(payload, 0, command.Param1);
        WriteSingle(payload, 4, command.Param2);
        WriteSingle(payload, 8, command.Param3);
        WriteSingle(payload, 12, command.Param4);
        WriteSingle(payload, 16, command.Param5);
        WriteSingle(payload, 20, command.Param6);
        WriteSingle(payload, 24, command.Param7);
        BitConverter.GetBytes(command.Command).CopyTo(payload, 28);
        payload[30] = command.TargetSystemId;
        payload[31] = command.TargetComponentId;
        payload[32] = command.Confirmation;

        return CreateV1Frame(systemId, componentId, messageId: 76, payload, crcExtra: 152);
    }

    public byte[] CreateSetMode(byte systemId, byte componentId, MavlinkSetMode setMode)
    {
        var payload = new byte[6];
        BitConverter.GetBytes(setMode.CustomMode).CopyTo(payload, 0);
        payload[4] = setMode.TargetSystemId;
        payload[5] = setMode.BaseMode;
        return CreateV1Frame(systemId, componentId, messageId: 11, payload, crcExtra: 89);
    }

    public byte[] CreateParamRequestList(byte systemId, byte componentId, MavlinkParameterRequestList request)
    {
        var payload = new byte[2];
        payload[0] = request.TargetSystemId;
        payload[1] = request.TargetComponentId;
        return CreateV1Frame(systemId, componentId, messageId: 21, payload, crcExtra: 159);
    }

    public byte[] CreateParamRequestRead(byte systemId, byte componentId, MavlinkParameterRequestRead request)
    {
        var payload = new byte[20];
        BitConverter.GetBytes(request.Index).CopyTo(payload, 0);
        payload[2] = request.TargetSystemId;
        payload[3] = request.TargetComponentId;
        WriteParamName(payload, 4, request.Name);
        return CreateV1Frame(systemId, componentId, messageId: 20, payload, crcExtra: 214);
    }

    public byte[] CreateParamSet(byte systemId, byte componentId, MavlinkParameterSet parameterSet)
    {
        var payload = new byte[23];
        WriteSingle(payload, 0, parameterSet.Value);
        payload[4] = parameterSet.TargetSystemId;
        payload[5] = parameterSet.TargetComponentId;
        WriteParamName(payload, 6, parameterSet.Name);
        payload[22] = (byte)parameterSet.Type;
        return CreateV1Frame(systemId, componentId, messageId: 23, payload, crcExtra: 168);
    }

    public byte[] CreateMissionRequestList(byte systemId, byte componentId, MavlinkMissionRequestList request)
    {
        var payload = CreateMissionPayload(baseLength: 2, request.MissionType);
        payload[0] = request.TargetSystemId;
        payload[1] = request.TargetComponentId;
        return CreateMissionFrame(systemId, componentId, messageId: 43, payload, crcExtra: 132, request.MissionType);
    }

    public byte[] CreateMissionCount(byte systemId, byte componentId, MavlinkMissionCount count)
    {
        var payload = CreateMissionPayload(baseLength: 4, count.MissionType);
        BitConverter.GetBytes(count.Count).CopyTo(payload, 0);
        payload[2] = count.TargetSystemId;
        payload[3] = count.TargetComponentId;
        return CreateMissionFrame(systemId, componentId, messageId: 44, payload, crcExtra: 221, count.MissionType);
    }

    public byte[] CreateMissionRequestInt(byte systemId, byte componentId, MavlinkMissionRequestInt request)
    {
        var payload = CreateMissionPayload(baseLength: 4, request.MissionType);
        BitConverter.GetBytes(request.Sequence).CopyTo(payload, 0);
        payload[2] = request.TargetSystemId;
        payload[3] = request.TargetComponentId;
        return CreateMissionFrame(systemId, componentId, messageId: 51, payload, crcExtra: 196, request.MissionType);
    }

    public byte[] CreateMissionItemInt(byte systemId, byte componentId, MavlinkMissionItemInt item)
    {
        var payload = CreateMissionPayload(baseLength: 37, item.MissionType);
        WriteSingle(payload, 0, item.Param1);
        WriteSingle(payload, 4, item.Param2);
        WriteSingle(payload, 8, item.Param3);
        WriteSingle(payload, 12, item.Param4);
        BitConverter.GetBytes(item.X).CopyTo(payload, 16);
        BitConverter.GetBytes(item.Y).CopyTo(payload, 20);
        WriteSingle(payload, 24, item.Z);
        BitConverter.GetBytes(item.Sequence).CopyTo(payload, 28);
        BitConverter.GetBytes(item.Command).CopyTo(payload, 30);
        payload[32] = item.TargetSystemId;
        payload[33] = item.TargetComponentId;
        payload[34] = item.Frame;
        payload[35] = item.Current;
        payload[36] = item.AutoContinue;
        return CreateMissionFrame(systemId, componentId, messageId: 73, payload, crcExtra: 38, item.MissionType);
    }

    public byte[] CreateMissionAck(byte systemId, byte componentId, MavlinkMissionAck ack)
    {
        var payload = CreateMissionPayload(baseLength: 3, ack.MissionType);
        payload[0] = ack.TargetSystemId;
        payload[1] = ack.TargetComponentId;
        payload[2] = (byte)ack.Result;
        return CreateMissionFrame(systemId, componentId, messageId: 47, payload, crcExtra: 153, ack.MissionType);
    }

    public byte[] CreateMissionClearAll(byte systemId, byte componentId, MavlinkMissionClearAll clearAll)
    {
        var payload = CreateMissionPayload(baseLength: 2, clearAll.MissionType);
        payload[0] = clearAll.TargetSystemId;
        payload[1] = clearAll.TargetComponentId;
        return CreateMissionFrame(systemId, componentId, messageId: 45, payload, crcExtra: 232, clearAll.MissionType);
    }

    public byte[] CreateV1Frame(byte systemId, byte componentId, byte messageId, ReadOnlySpan<byte> payload, byte crcExtra)
    {
        var frame = new byte[6 + payload.Length + 2];
        frame[0] = 0xFE;
        frame[1] = (byte)payload.Length;
        frame[2] = _sequence++;
        frame[3] = systemId;
        frame[4] = componentId;
        frame[5] = messageId;
        payload.CopyTo(frame.AsSpan(6));

        var crc = MavlinkCrc.Accumulate(frame.AsSpan(1, 5 + payload.Length), crcExtra);
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    public byte[] CreateV2Frame(byte systemId, byte componentId, uint messageId, ReadOnlySpan<byte> payload, byte crcExtra)
    {
        var frame = new byte[10 + payload.Length + 2];
        frame[0] = 0xFD;
        frame[1] = (byte)payload.Length;
        frame[2] = 0;
        frame[3] = 0;
        frame[4] = _sequence++;
        frame[5] = systemId;
        frame[6] = componentId;
        frame[7] = (byte)(messageId & 0xFF);
        frame[8] = (byte)((messageId >> 8) & 0xFF);
        frame[9] = (byte)((messageId >> 16) & 0xFF);
        payload.CopyTo(frame.AsSpan(10));

        var crc = MavlinkCrc.Accumulate(frame.AsSpan(1, 9 + payload.Length), crcExtra);
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    private static byte[] CreateMissionPayload(int baseLength, MavMissionType missionType)
    {
        var payload = new byte[missionType == MavMissionType.Mission ? baseLength : baseLength + 1];
        if (payload.Length > baseLength)
        {
            payload[baseLength] = (byte)missionType;
        }

        return payload;
    }

    private byte[] CreateMissionFrame(byte systemId, byte componentId, byte messageId, byte[] payload, byte crcExtra, MavMissionType missionType)
    {
        return missionType == MavMissionType.Mission
            ? CreateV1Frame(systemId, componentId, messageId, payload, crcExtra)
            : CreateV2Frame(systemId, componentId, messageId, payload, crcExtra);
    }

    private static void WriteSingle(byte[] payload, int startIndex, float value)
    {
        BitConverter.GetBytes(value).CopyTo(payload, startIndex);
    }

    private static void WriteParamName(byte[] payload, int startIndex, string name)
    {
        var bytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(bytes, 0, payload, startIndex, Math.Min(bytes.Length, 16));
    }
}
