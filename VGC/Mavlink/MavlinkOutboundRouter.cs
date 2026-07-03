using VGC.Comms;

namespace VGC.Mavlink;

public sealed class MavlinkOutboundRouter
{
    private readonly MavlinkFrameWriter _frameWriter;

    public MavlinkOutboundRouter(MavlinkFrameWriter? frameWriter = null)
    {
        _frameWriter = frameWriter ?? new MavlinkFrameWriter();
    }

    public MavlinkFrameWriter FrameWriter => _frameWriter;

    public uint FramesSent { get; private set; }

    public event EventHandler<MavlinkOutboundFrameEventArgs>? FrameSent;

    public async ValueTask SendFrameAsync(
        ILinkTransport link,
        byte[] frame,
        string route,
        CancellationToken cancellationToken = default)
    {
        await link.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        FramesSent++;
        FrameSent?.Invoke(this, new MavlinkOutboundFrameEventArgs(link, route, frame));
    }

    public ValueTask SendGcsHeartbeatAsync(
        ILinkTransport link,
        byte systemId = 255,
        byte componentId = 190,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateGcsHeartbeat(systemId, componentId),
            "HEARTBEAT",
            cancellationToken);
    }

    public ValueTask SendCommandLongAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkCommandLong command,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateCommandLong(systemId, componentId, command),
            "COMMAND_LONG",
            cancellationToken);
    }

    public ValueTask SendSetModeAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkSetMode setMode,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateSetMode(systemId, componentId, setMode),
            "SET_MODE",
            cancellationToken);
    }

    public ValueTask SendParamRequestListAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkParameterRequestList request,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateParamRequestList(systemId, componentId, request),
            "PARAM_REQUEST_LIST",
            cancellationToken);
    }

    public ValueTask SendParamRequestReadAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkParameterRequestRead request,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateParamRequestRead(systemId, componentId, request),
            "PARAM_REQUEST_READ",
            cancellationToken);
    }

    public ValueTask SendParamSetAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkParameterSet parameterSet,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateParamSet(systemId, componentId, parameterSet),
            "PARAM_SET",
            cancellationToken);
    }

    public ValueTask SendMissionRequestListAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkMissionRequestList request,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateMissionRequestList(systemId, componentId, request),
            "MISSION_REQUEST_LIST",
            cancellationToken);
    }

    public ValueTask SendMissionCountAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkMissionCount count,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateMissionCount(systemId, componentId, count),
            "MISSION_COUNT",
            cancellationToken);
    }

    public ValueTask SendMissionRequestIntAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkMissionRequestInt request,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateMissionRequestInt(systemId, componentId, request),
            "MISSION_REQUEST_INT",
            cancellationToken);
    }

    public ValueTask SendMissionItemIntAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkMissionItemInt item,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateMissionItemInt(systemId, componentId, item),
            "MISSION_ITEM_INT",
            cancellationToken);
    }

    public ValueTask SendMissionAckAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkMissionAck ack,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateMissionAck(systemId, componentId, ack),
            "MISSION_ACK",
            cancellationToken);
    }

    public ValueTask SendMissionClearAllAsync(
        ILinkTransport link,
        byte systemId,
        byte componentId,
        MavlinkMissionClearAll clearAll,
        CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(
            link,
            _frameWriter.CreateMissionClearAll(systemId, componentId, clearAll),
            "MISSION_CLEAR_ALL",
            cancellationToken);
    }
}

public sealed record MavlinkOutboundFrameEventArgs(
    ILinkTransport Link,
    string Route,
    byte[] Frame);
