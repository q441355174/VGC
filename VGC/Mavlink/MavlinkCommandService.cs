using VGC.Comms;

namespace VGC.Mavlink;

public sealed class MavlinkCommandService
{
    private readonly MavlinkFrameWriter _frameWriter;
    private readonly MavlinkOutboundRouter _outboundRouter;

    public MavlinkCommandService(
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

    public byte[] CreateCommandLongFrame(MavlinkCommandLong command)
    {
        return _frameWriter.CreateCommandLong(SystemId, ComponentId, command);
    }

    public ValueTask SendCommandLongAsync(ILinkTransport link, MavlinkCommandLong command, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendCommandLongAsync(link, SystemId, ComponentId, command, cancellationToken);
    }

    public static bool TryReadCommandAck(MavlinkPacket packet, out MavlinkCommandAck ack)
    {
        ack = default!;
        if (packet.MessageId != 77 || packet.Payload.Length < 3)
        {
            return false;
        }

        var command = BitConverter.ToUInt16(packet.Payload, 0);
        var result = (MavlinkCommandResult)packet.Payload[2];
        ack = new MavlinkCommandAck(packet.SystemId, packet.ComponentId, command, result);
        return true;
    }
}
