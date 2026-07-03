using VGC.Comms;

namespace VGC.Mavlink;

public enum MavlinkMissionResult : byte
{
    Accepted = 0,
    Error = 1,
    UnsupportedFrame = 2,
    Unsupported = 3,
    NoSpace = 4,
    Invalid = 5,
    InvalidParam1 = 6,
    InvalidParam2 = 7,
    InvalidParam3 = 8,
    InvalidParam4 = 9,
    InvalidParam5X = 10,
    InvalidParam6Y = 11,
    InvalidParam7 = 12,
    InvalidSequence = 13,
    Denied = 14,
    OperationCancelled = 15
}

public enum MavMissionType : byte
{
    Mission = 0,
    Fence = 1,
    Rally = 2,
    All = 255
}

public sealed record MavlinkMissionRequestList(
    byte TargetSystemId,
    byte TargetComponentId,
    MavMissionType MissionType = MavMissionType.Mission);

public sealed record MavlinkMissionCount(
    byte TargetSystemId,
    byte TargetComponentId,
    ushort Count,
    MavMissionType MissionType = MavMissionType.Mission);

public sealed record MavlinkMissionRequestInt(
    byte TargetSystemId,
    byte TargetComponentId,
    ushort Sequence,
    MavMissionType MissionType = MavMissionType.Mission);

public sealed record MavlinkMissionItemInt(
    byte TargetSystemId,
    byte TargetComponentId,
    ushort Sequence,
    ushort Command,
    byte Frame,
    byte Current,
    byte AutoContinue,
    float Param1,
    float Param2,
    float Param3,
    float Param4,
    int X,
    int Y,
    float Z,
    MavMissionType MissionType = MavMissionType.Mission);

public sealed record MavlinkMissionAck(
    byte TargetSystemId,
    byte TargetComponentId,
    MavlinkMissionResult Result,
    MavMissionType MissionType = MavMissionType.Mission);

public sealed record MavlinkMissionClearAll(
    byte TargetSystemId,
    byte TargetComponentId,
    MavMissionType MissionType = MavMissionType.Mission);

public sealed class MavlinkMissionService
{
    private readonly MavlinkFrameWriter _frameWriter;
    private readonly MavlinkOutboundRouter _outboundRouter;

    public MavlinkMissionService(
        byte systemId = 255,
        byte componentId = 190,
        MavlinkFrameWriter? frameWriter = null,
        MavlinkOutboundRouter? outboundRouter = null)
    {
        SystemId = systemId;
        ComponentId = componentId;
        _outboundRouter = outboundRouter ?? new MavlinkOutboundRouter(frameWriter);
        _frameWriter = _outboundRouter.FrameWriter;
    }

    public byte SystemId { get; }

    public byte ComponentId { get; }

    public byte[] CreateMissionRequestListFrame(MavlinkMissionRequestList request)
    {
        return _frameWriter.CreateMissionRequestList(SystemId, ComponentId, request);
    }

    public byte[] CreateMissionCountFrame(MavlinkMissionCount count)
    {
        return _frameWriter.CreateMissionCount(SystemId, ComponentId, count);
    }

    public byte[] CreateMissionRequestIntFrame(MavlinkMissionRequestInt request)
    {
        return _frameWriter.CreateMissionRequestInt(SystemId, ComponentId, request);
    }

    public byte[] CreateMissionItemIntFrame(MavlinkMissionItemInt item)
    {
        return _frameWriter.CreateMissionItemInt(SystemId, ComponentId, item);
    }

    public byte[] CreateMissionAckFrame(MavlinkMissionAck ack)
    {
        return _frameWriter.CreateMissionAck(SystemId, ComponentId, ack);
    }

    public byte[] CreateMissionClearAllFrame(MavlinkMissionClearAll clearAll)
    {
        return _frameWriter.CreateMissionClearAll(SystemId, ComponentId, clearAll);
    }

    public ValueTask SendMissionRequestListAsync(ILinkTransport link, MavlinkMissionRequestList request, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendMissionRequestListAsync(link, SystemId, ComponentId, request, cancellationToken);
    }

    public ValueTask SendMissionCountAsync(ILinkTransport link, MavlinkMissionCount count, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendMissionCountAsync(link, SystemId, ComponentId, count, cancellationToken);
    }

    public ValueTask SendMissionRequestIntAsync(ILinkTransport link, MavlinkMissionRequestInt request, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendMissionRequestIntAsync(link, SystemId, ComponentId, request, cancellationToken);
    }

    public ValueTask SendMissionItemIntAsync(ILinkTransport link, MavlinkMissionItemInt item, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendMissionItemIntAsync(link, SystemId, ComponentId, item, cancellationToken);
    }

    public ValueTask SendMissionAckAsync(ILinkTransport link, MavlinkMissionAck ack, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendMissionAckAsync(link, SystemId, ComponentId, ack, cancellationToken);
    }

    public ValueTask SendMissionClearAllAsync(ILinkTransport link, MavlinkMissionClearAll clearAll, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendMissionClearAllAsync(link, SystemId, ComponentId, clearAll, cancellationToken);
    }

    public static bool TryReadMissionRequestList(MavlinkPacket packet, out MavlinkMissionRequestList request)
    {
        request = default!;
        if (packet.MessageId != 43 || packet.Payload.Length < 2)
        {
            return false;
        }

        request = new MavlinkMissionRequestList(
            TargetSystemId: packet.Payload[0],
            TargetComponentId: packet.Payload[1],
            MissionType: ReadMissionType(packet.Payload, 2));
        return true;
    }

    public static bool TryReadMissionCount(MavlinkPacket packet, out MavlinkMissionCount count)
    {
        count = default!;
        if (packet.MessageId != 44 || packet.Payload.Length < 4)
        {
            return false;
        }

        count = new MavlinkMissionCount(
            TargetSystemId: packet.Payload[2],
            TargetComponentId: packet.Payload[3],
            Count: BitConverter.ToUInt16(packet.Payload, 0),
            MissionType: ReadMissionType(packet.Payload, 4));
        return true;
    }

    public static bool TryReadMissionRequestInt(MavlinkPacket packet, out MavlinkMissionRequestInt request)
    {
        request = default!;
        if (packet.MessageId != 51 || packet.Payload.Length < 4)
        {
            return false;
        }

        request = new MavlinkMissionRequestInt(
            TargetSystemId: packet.Payload[2],
            TargetComponentId: packet.Payload[3],
            Sequence: BitConverter.ToUInt16(packet.Payload, 0),
            MissionType: ReadMissionType(packet.Payload, 4));
        return true;
    }

    public static bool TryReadMissionItemInt(MavlinkPacket packet, out MavlinkMissionItemInt item)
    {
        item = default!;
        if (packet.MessageId != 73 || packet.Payload.Length < 37)
        {
            return false;
        }

        item = new MavlinkMissionItemInt(
            TargetSystemId: packet.Payload[32],
            TargetComponentId: packet.Payload[33],
            Sequence: BitConverter.ToUInt16(packet.Payload, 28),
            Command: BitConverter.ToUInt16(packet.Payload, 30),
            Frame: packet.Payload[34],
            Current: packet.Payload[35],
            AutoContinue: packet.Payload[36],
            Param1: BitConverter.ToSingle(packet.Payload, 0),
            Param2: BitConverter.ToSingle(packet.Payload, 4),
            Param3: BitConverter.ToSingle(packet.Payload, 8),
            Param4: BitConverter.ToSingle(packet.Payload, 12),
            X: BitConverter.ToInt32(packet.Payload, 16),
            Y: BitConverter.ToInt32(packet.Payload, 20),
            Z: BitConverter.ToSingle(packet.Payload, 24),
            MissionType: ReadMissionType(packet.Payload, 37));
        return true;
    }

    public static bool TryReadMissionAck(MavlinkPacket packet, out MavlinkMissionAck ack)
    {
        ack = default!;
        if (packet.MessageId != 47 || packet.Payload.Length < 3)
        {
            return false;
        }

        ack = new MavlinkMissionAck(
            TargetSystemId: packet.Payload[0],
            TargetComponentId: packet.Payload[1],
            Result: (MavlinkMissionResult)packet.Payload[2],
            MissionType: ReadMissionType(packet.Payload, 3));
        return true;
    }

    private static MavMissionType ReadMissionType(byte[] payload, int index)
    {
        return payload.Length > index ? (MavMissionType)payload[index] : MavMissionType.Mission;
    }
}
