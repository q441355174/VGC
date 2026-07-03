using VGC.Comms;

namespace VGC.Mavlink;

public sealed record MavlinkSetMode(
    byte TargetSystemId,
    byte BaseMode,
    uint CustomMode);

public sealed class MavlinkModeService
{
    private readonly MavlinkFrameWriter _frameWriter;
    private readonly MavlinkOutboundRouter _outboundRouter;

    public MavlinkModeService(
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

    public byte[] CreateSetModeFrame(MavlinkSetMode setMode)
    {
        return _frameWriter.CreateSetMode(SystemId, ComponentId, setMode);
    }

    public ValueTask SendSetModeAsync(ILinkTransport link, MavlinkSetMode setMode, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendSetModeAsync(link, SystemId, ComponentId, setMode, cancellationToken);
    }
}
